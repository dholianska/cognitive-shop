using System.Diagnostics;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();   // http://orders-api -> реальна адреса від Aspire

var app = builder.Build();

app.UseCorrelationId();   // ДО MapReverseProxy: генерує/зберігає X-Correlation-Id

// Логування рішень маршрутизації
app.Use(async (context, next) =>
{
    var sw = Stopwatch.StartNew();
    await next();

    var proxy = context.GetReverseProxyFeature();
    var route = proxy?.Route.Config.RouteId;
    var destination = proxy?.ProxiedDestination?.Model.Config.Address;

    app.Logger.LogInformation(
        "{Method} {Path} -> route {Route} ({Destination}): {StatusCode} in {Elapsed} ms, client {ClientIp}",
        context.Request.Method, context.Request.Path, route, destination,
        context.Response.StatusCode, sw.ElapsedMilliseconds, context.Connection.RemoteIpAddress);

    var error = context.Features.Get<IForwarderErrorFeature>();
    if (error is not null)
        app.Logger.LogWarning(error.Exception, "Proxy error {Error} on route {Route}", error.Error, route);
});

app.MapReverseProxy();
app.MapDefaultEndpoints();

app.Run();