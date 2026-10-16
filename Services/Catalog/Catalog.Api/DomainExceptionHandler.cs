using Catalog.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api;

/// <summary>
/// Єдине місце, де винятки перетворюються на HTTP-статуси у форматі ProblemDetails.
/// Кожен виняток логується один раз саме тут.
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

        var (status, title) = ex switch
        {
            EntityNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            DomainException => (StatusCodes.Status409Conflict, "Business rule violated"),
            // два паралельні запити з однаковим SKU: перевірку в сервісі "обійшли", але унікальний індекс захистив
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } }
                => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(ex, "Unhandled exception for {Path}", http.Request.Path);
        else
            logger.LogWarning("{Title} for {Path}: {Message}", title, http.Request.Path, ex.Message);

        // для 4xx повідомлення наших винятків безпечне; деталі 500 показуємо лише в Development
        string? detail;
        if (ex is DbUpdateException) detail = "A record with the same unique key already exists";
        else if (status < 500 || env.IsDevelopment()) detail = ex.Message;
        else detail = null;

        http.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = ex,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail }
        });
    }
}