using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Infrastructure.Persistence;

namespace Reviews.WebApi.HealthChecks;

/// <summary>Легка перевірка MongoDB: команда ping. Без побічних ефектів.</summary>
public sealed class MongoHealthCheck(MongoDbContext context) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext healthContext, CancellationToken ct = default)
    {
        try
        {
            await context.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: ct);
            return HealthCheckResult.Healthy("MongoDB is reachable");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)   // таймаут реєстрації обробляє фреймворк
        {
            return new HealthCheckResult(healthContext.Registration.FailureStatus, "MongoDB is unreachable", ex);
        }
    }
}