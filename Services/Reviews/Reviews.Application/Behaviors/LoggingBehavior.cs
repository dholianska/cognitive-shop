using MediatR;
using Microsoft.Extensions.Logging;

namespace Reviews.Application.Behaviors;

/// <summary>
/// Структуроване логування кожного запиту. Якщо валідація зупинила запит, у лозі є "Handling" і
/// "failed", але немає "Handled": це доказ, що обробник не виконувався.
/// Стек винятку тут не пишеться: кожен виняток докладно логує глобальний обробник один раз.
/// </summary>
public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        logger.LogInformation("Handling {RequestName} {@Request}", name, request);

        try
        {
            var response = await next(ct);
            logger.LogInformation("Handled {RequestName}", name);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogWarning("{RequestName} failed with {ExceptionType}: {Reason}",
                name, ex.GetType().Name, ex.Message);
            throw;
        }
    }
}