using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Reviews.Application.Behaviors;

/// <summary>Вимірює час виконання обробника й попереджає про аномально повільні запити.</summary>
public class PerformanceBehavior<TRequest, TResponse>(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private const int SlowThresholdMs = 500;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var response = await next(ct);
        sw.Stop();

        if (sw.ElapsedMilliseconds > SlowThresholdMs)
            logger.LogWarning("Slow request {RequestName}: {ElapsedMs} ms", typeof(TRequest).Name, sw.ElapsedMilliseconds);

        return response;
    }
}