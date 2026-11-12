using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Platform.ServiceDefaults;

/// <summary>JSON-відповідь health check з деталями кожної перевірки.</summary>
public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static Task WriteJsonAsync(HttpContext context, HealthReport report)
    {
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 1),
                tags = e.Value.Tags,
                error = e.Value.Exception?.Message
            })
        };

        return context.Response.WriteAsJsonAsync(payload, Options);
    }
}