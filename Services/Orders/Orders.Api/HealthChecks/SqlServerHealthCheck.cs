using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Orders.Api.HealthChecks;

/// <summary>Легка перевірка SQL Server: з'єднання + SELECT 1. Без побічних ефектів.</summary>
public sealed class SqlServerHealthCheck(string connectionString) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);

            await using var command = new SqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(ct);

            return HealthCheckResult.Healthy("SQL Server is reachable");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)   // таймаут реєстрації обробляє фреймворк
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "SQL Server is unreachable", ex);
        }
    }
}