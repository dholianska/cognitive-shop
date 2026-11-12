using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Reviews.WebApi.HealthChecks;

/// <summary>Перевірка Redis: легке читання через IDistributedCache з жорстким таймаутом 3 с.</summary>
public sealed class RedisHealthCheck(IDistributedCache cache) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            // ключа може не існувати: важлива лише відповідь сервера Redis
            await cache.GetAsync("health:probe", ct).WaitAsync(TimeSpan.FromSeconds(3), ct);
            return HealthCheckResult.Healthy("Redis is reachable");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // кеш необов'язковий: сервіс працює без нього, тому Degraded, а не Unhealthy
            return new HealthCheckResult(context.Registration.FailureStatus,
                "Redis is unavailable, the service works without cache", ex);
        }
    }
}