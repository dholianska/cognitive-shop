using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Platform.Aggregator.HealthChecks;

/// <summary>Перевірка Redis: легке читання через IDistributedCache з жорстким таймаутом 3 с.</summary>
public sealed class RedisHealthCheck(IDistributedCache cache) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await cache.GetAsync("health:probe", ct).WaitAsync(TimeSpan.FromSeconds(3), ct);
            return HealthCheckResult.Healthy("Redis is reachable");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus,
                "Redis is unavailable, aggregated responses are not cached", ex);
        }
    }
}