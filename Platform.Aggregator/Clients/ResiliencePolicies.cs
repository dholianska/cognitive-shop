
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Platform.Aggregator.Clients;

/// <summary>Профіль resilience клієнта. Обґрунтування значень: README, розділ «Лаб. 07».</summary>
public sealed record ResilienceProfile(
    int Retries,
    TimeSpan RetryDelay,
    TimeSpan AttemptTimeout,
    TimeSpan TotalTimeout,
    double FailureRatio,
    int MinimumThroughput,
    TimeSpan BreakDuration);

public static class ResiliencePolicies
{
    /// <summary>Критичні сервіси: більше повторів, чутливіший circuit breaker.</summary>
    public static readonly ResilienceProfile Critical = new(
        Retries: 4,
        RetryDelay: TimeSpan.FromSeconds(0.5),
        AttemptTimeout: TimeSpan.FromSeconds(5),
        TotalTimeout: TimeSpan.FromSeconds(35),     // 5 спроб × 5 с + затримки між ними (≈ 7,5 с) з запасом
        FailureRatio: 0.3,
        MinimumThroughput: 10,                      // навчальний стенд: мало запитів
        BreakDuration: TimeSpan.FromSeconds(15));

    /// <summary>Некритичні сервіси: швидко здатися й повернути часткову відповідь.</summary>
    public static readonly ResilienceProfile NonCritical = new(
        Retries: 1,
        RetryDelay: TimeSpan.FromSeconds(0.5),
        AttemptTimeout: TimeSpan.FromSeconds(2),
        TotalTimeout: TimeSpan.FromSeconds(5),      // 2 спроби × 2 с + затримка
        FailureRatio: 0.7,                          // менш чутливий breaker
        MinimumThroughput: 10,
        BreakDuration: TimeSpan.FromSeconds(10));

    /// <summary>
    /// Для gRPC-клієнтів Polly повторює лише мережеві збої, а повтори за gRPC-статусами робить
    /// сам канал (ServiceConfig). Щоб повтори не множились, на рівні Polly лишаємо один.
    /// </summary>
    public static ResilienceProfile ForGrpc(this ResilienceProfile profile) => profile with { Retries = 1 };

    /// <summary>Логер для переходів circuit breaker; ініціалізується в Program.cs після Build().</summary>
    public static ILogger? Logger { get; set; }

    /// <summary>
    /// Прибирає стандартний обробник з ServiceDefaults і додає власний, щоб обробники не нашаровувались.
    /// </summary>
#pragma warning disable EXTEXP0001
    public static IHttpClientBuilder AddResilience(
        this IHttpClientBuilder builder, string service, ResilienceProfile profile)
    {
        builder.RemoveAllResilienceHandlers();

        builder.AddStandardResilienceHandler(o =>
        {
            // Retry: експоненційна затримка + jitter проти thundering herd;
            // небезпечні методи (POST/PUT/DELETE) не повторюємо: можна створити дубль
            o.Retry.MaxRetryAttempts = profile.Retries;
            o.Retry.Delay = profile.RetryDelay;
            o.Retry.BackoffType = DelayBackoffType.Exponential;
            o.Retry.UseJitter = true;
            o.Retry.DisableForUnsafeHttpMethods();

            // Таймаути: загальний має вміщати всі повтори
            o.AttemptTimeout.Timeout = profile.AttemptTimeout;
            o.TotalRequestTimeout.Timeout = profile.TotalTimeout;

            // Circuit breaker: SamplingDuration >= 2 × AttemptTimeout, інакше валідація опцій впаде при старті
            o.CircuitBreaker.FailureRatio = profile.FailureRatio;
            o.CircuitBreaker.MinimumThroughput = profile.MinimumThroughput;
            o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            o.CircuitBreaker.BreakDuration = profile.BreakDuration;

            o.CircuitBreaker.OnOpened = args =>
            {
                Logger?.LogError(
                    "Circuit breaker for {Service} OPENED for {BreakDuration} s, reason {Reason}",
                    service, args.BreakDuration.TotalSeconds,
                    args.Outcome.Exception?.GetType().Name ?? args.Outcome.Result?.StatusCode.ToString());
                return ValueTask.CompletedTask;
            };
            o.CircuitBreaker.OnHalfOpened = _ =>
            {
                Logger?.LogWarning("Circuit breaker for {Service} HALF-OPEN, sending a probe request", service);
                return ValueTask.CompletedTask;
            };
            o.CircuitBreaker.OnClosed = _ =>
            {
                Logger?.LogInformation("Circuit breaker for {Service} CLOSED, the service has recovered", service);
                return ValueTask.CompletedTask;
            };
        });

        return builder;
    }
#pragma warning restore EXTEXP0001
}
