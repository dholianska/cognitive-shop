using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Reviews.Domain.Exceptions;

namespace Reviews.WebApi;

/// <summary>
/// Єдине місце, де винятки (доменні, FluentValidation і MongoDB-специфічні) перетворюються на
/// HTTP-статуси у форматі ProblemDetails. Кожен виняток логується один раз саме тут.
/// </summary>
public sealed class DomainExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<DomainExceptionHandler> logger,
    IHostEnvironment env) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        // клієнт сам скасував запит: помилки сервера тут немає
        if (ex is OperationCanceledException) return true;

        // помилки валідації: 400 зі словником "поле -> повідомлення" (ValidationProblemDetails)
        if (ex is ValidationException validation)
        {
            var errors = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

            logger.LogWarning("Validation failed for {Path}: {Fields}", http.Request.Path, string.Join(", ", errors.Keys));

            await Results.ValidationProblem(
                    errors,
                    title: "Validation failed",
                    extensions: new Dictionary<string, object?> { ["traceId"] = http.TraceIdentifier })
                .ExecuteAsync(http);
            return true;
        }

        (int Status, string Title, string? Detail) result = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found", ex.Message),
            DomainException => (StatusCodes.Status400BadRequest, "Business rule violated", ex.Message),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict", ex.Message),

            // унікальний індекс (дубль відгуку при гонці двох запитів)
            MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey }
                => (StatusCodes.Status409Conflict, "Conflict", "A review with the same unique key already exists"),
            MongoWriteException
                => (StatusCodes.Status400BadRequest, "Write failed", "The database rejected the write operation"),

            // база недоступна
            MongoConnectionException or TimeoutException
                => (StatusCodes.Status503ServiceUnavailable, "Service unavailable", "Database is temporarily unavailable"),

            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error",
                  env.IsDevelopment() ? ex.Message : null)
        };

        if (result.Status >= 500)
            logger.LogError(ex, "Request {Path} failed with {Status}", http.Request.Path, result.Status);
        else
            logger.LogWarning("{Title} for {Path}: {Message}", result.Title, http.Request.Path, ex.Message);

        http.Response.StatusCode = result.Status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = ex,
            ProblemDetails = new ProblemDetails { Status = result.Status, Title = result.Title, Detail = result.Detail }
        });
    }
}