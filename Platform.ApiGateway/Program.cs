using System.Diagnostics;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Yarp.ReverseProxy.Transforms;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Лаб. 09, Ч.1: Gateway валідує JWT ДО проксіювання (маршрути з AuthorizationPolicy "authenticated")
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = AuthSchemes.OwnJwtParameters(builder.Configuration));
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("authenticated", p => p.RequireAuthenticatedUser());

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver()   // http://orders-api -> реальна адреса від Aspire
        .AddTransforms(ctx => ctx.AddRequestTransform(t =>
        {
            // Клієнтському X-User-Id не довіряємо: прибираємо й виставляємо з перевіреного токена
            t.ProxyRequest.Headers.Remove("X-User-Id");
            if (t.HttpContext.User.GetUserId() is { } userId)
                t.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Id", userId);
            return ValueTask.CompletedTask;
        }));

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


app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();
app.MapDefaultEndpoints();

app.Run();