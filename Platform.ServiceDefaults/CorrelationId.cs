using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace Platform.ServiceDefaults;

public class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var generated = false;
        if (!context.Request.Headers.TryGetValue(HeaderName, out var value) || string.IsNullOrEmpty(value))
        {
            value = Guid.NewGuid().ToString();
            context.Request.Headers[HeaderName] = value;   // далі його побачать YARP і HttpClient
            generated = true;
        }

        var id = value.ToString();
        context.Response.Headers[HeaderName] = id;
        Activity.Current?.SetTag("correlation.id", id);   // щоб trace теж можна було знайти за CorrelationId

        using (LogContext.PushProperty("CorrelationId", id))
        {
            if (generated)
                logger.LogInformation("Generated new CorrelationId {CorrelationId}", id);

            var sw = Stopwatch.StartNew();
            logger.LogInformation("Request started {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await next(context);

            logger.LogInformation("Request finished with {StatusCode} in {Elapsed} ms",
                context.Response.StatusCode, sw.ElapsedMilliseconds);
        }
    }
}

public class CorrelationIdHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var id = accessor.HttpContext?.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        if (!string.IsNullOrEmpty(id))
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, id);
        return base.SendAsync(request, ct);
    }
}