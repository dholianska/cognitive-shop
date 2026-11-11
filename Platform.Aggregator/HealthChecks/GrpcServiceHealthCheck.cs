
using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Platform.Aggregator.HealthChecks;

/// <summary>Перевіряє downstream-сервіс стандартним gRPC Health Checking Protocol.</summary>
public sealed class GrpcServiceHealthCheck(
    string service, GrpcClientFactory factory, ILogger<GrpcServiceHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var client = factory.CreateClient<Health.HealthClient>(service);
            var reply = await client.CheckAsync(
                new HealthCheckRequest(), deadline: DateTime.UtcNow.AddSeconds(2), cancellationToken: ct);

            return reply.Status == HealthCheckResponse.Types.ServingStatus.Serving
                ? HealthCheckResult.Healthy($"{service} is serving")
                : new HealthCheckResult(context.Registration.FailureStatus, $"{service} status: {reply.Status}");
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "{Service} gRPC health check failed ({Status})", service, ex.StatusCode);
            return new HealthCheckResult(context.Registration.FailureStatus, $"{service} is unavailable", ex);
        }
    }
}

public static class DownstreamHealthChecksExtensions
{
    /// <summary>Реєструє gRPC Health-клієнти й перевірки downstream-сервісів.</summary>
#pragma warning disable EXTEXP0001
    public static IServiceCollection AddDownstreamHealthChecks(this IServiceCollection services)
    {
        // клієнти для health-перевірок без resilience: проба має відповідати швидко і не міняти стан breaker-ів
        foreach (var name in new[] { "orders-api", "catalog-api", "reviews-api" })
        {
            services.AddGrpcClient<Health.HealthClient>(name, o => o.Address = new Uri($"https://{name}"))
                .RemoveAllResilienceHandlers();
        }

        services.AddHealthChecks()
            .AddTypeActivatedCheck<GrpcServiceHealthCheck>("orders-api", HealthStatus.Unhealthy,
                ["grpc", "downstream", "ready"], TimeSpan.FromSeconds(3), "orders-api")
            .AddTypeActivatedCheck<GrpcServiceHealthCheck>("catalog-api", HealthStatus.Unhealthy,
                ["grpc", "downstream", "ready"], TimeSpan.FromSeconds(3), "catalog-api")
            .AddTypeActivatedCheck<GrpcServiceHealthCheck>("reviews-api", HealthStatus.Degraded,   // некритичний
                ["grpc", "downstream", "ready"], TimeSpan.FromSeconds(3), "reviews-api")
            .AddCheck<RedisHealthCheck>("redis-cache",
                failureStatus: HealthStatus.Degraded,
                tags: ["cache", "redis", "ready"],
                timeout: TimeSpan.FromSeconds(5));

        return services;
    }
#pragma warning restore EXTEXP0001
}
