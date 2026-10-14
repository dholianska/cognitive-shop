using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Orders.Domain.Exceptions;

namespace Orders.Api;

/// <summary>
/// Єдине місце, де винятки перетворюються на HTTP-статуси у форматі ProblemDetails.
/// Ланцюжок: THROW 5000x у процедурі -> SqlException -> DAL (SqlErrors) -> доменний виняток -> тут.
/// </summary>
public sealed class DomainExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        // клієнт сам скасував запит: помилки сервера тут немає
        if (ex is OperationCanceledException) return true;

        var (status, title) = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            BusinessConflictException => (StatusCodes.Status409Conflict, "Business rule conflict"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error")
        };

        // кожен виняток логується один раз, саме тут
        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(ex, "Unhandled exception for {Path}", http.Request.Path);
        else
            logger.LogWarning("{Title} for {Path}: {Message}", title, http.Request.Path, ex.Message);

        http.Response.StatusCode = status;

        // для 4xx повідомлення нашого винятку безпечне; для 500 деталі клієнту не показуємо
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status < 500 ? ex.Message : null
        };

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = ex,
            ProblemDetails = problem
        });
    }
}