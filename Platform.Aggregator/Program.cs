using System.Text.Json.Serialization;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Metrics;
using Platform.Aggregator.Services;
using Platform.Aggregator.HealthChecks;
using Platform.ServiceDefaults;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Лаб. 09, Ч.2: приймаємо і власний JWT (Ч.1), і токени IdentityServer (Ч.2); схему обирає політика-схема за issuer
builder.Services.AddAuthentication(o =>
{
    o.DefaultScheme = AuthSchemes.Multi;
    o.DefaultChallengeScheme = AuthSchemes.Multi;
})
.AddPolicyScheme(AuthSchemes.Multi, "Own JWT or IdentityServer",
    o => o.ForwardDefaultSelector = ctx => AuthSchemes.SelectByIssuer(ctx, builder.Configuration))
.AddJwtBearer(AuthSchemes.OwnJwt,
    o => o.TokenValidationParameters = AuthSchemes.OwnJwtParameters(builder.Configuration))
.AddJwtBearer(AuthSchemes.IdentityServer, o =>
{
    o.Authority = builder.Configuration["IdentityServer:Authority"];   // ключі підпису з /.well-known/openid-configuration
    o.Audience = "aggregator_api";                                     // ApiResource цього сервісу
    o.RequireHttpsMetadata = false;                                    // розробка: IdentityServer на HTTP
});

// Scope-based політики: дозвіл дає scope з токена IdentityServer або токен власного Auth Service
var ownIssuer = builder.Configuration["Jwt:Issuer"];
builder.Services.AddAuthorizationBuilder()
        .AddPolicy("aggregator.read", p => p.RequireAssertion(c => c.User.HasScopeOrOwnJwt("aggregator.read", ownIssuer)));


builder.Services.AddHttpClient("identityserver");
builder.Services.AddSingleton<Platform.Aggregator.Clients.ServiceTokenProvider>();
builder.Services.AddTransient<Platform.Aggregator.Clients.ServiceTokenHandler>();

builder.AddRedisDistributedCache("redis", s => s.DisableHealthChecks = true);   // власну перевірку (Degraded) реєструємо нижче

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);
builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

builder.Services.AddOpenApi();

// gRPC-клієнти (основний шлях)
builder.Services.AddGrpcClients();
builder.Services.AddDownstreamHealthChecks();   // gRPC Health Protocol для сервісів + Redis (Degraded)

// HTTP-клієнти з Лаб. 05 (лишені для порівняння HTTP і gRPC)
builder.Services.AddHttpClient<OrdersClient>(c => c.BaseAddress = new("https+http://orders-api"))
    .AddResilience("orders-api", ResiliencePolicies.Critical)
    .AddHttpMessageHandler<ServiceTokenHandler>();
builder.Services.AddHttpClient<CatalogClient>(c => c.BaseAddress = new("https+http://catalog-api"))
    .AddResilience("catalog-api", ResiliencePolicies.Critical)
    .AddHttpMessageHandler<ServiceTokenHandler>();
builder.Services.AddHttpClient<ReviewsClient>(c => c.BaseAddress = new("https+http://reviews-api"))
    .AddResilience("reviews-api", ResiliencePolicies.NonCritical)
    .AddHttpMessageHandler<ServiceTokenHandler>();

builder.Services.AddSingleton<AggregatorCacheMetrics>();
builder.Services.AddScoped<OrderAggregationService>();        // HTTP (Лаб. 05)
builder.Services.AddScoped<GrpcOrderAggregationService>();    // gRPC
builder.Services.AddScoped<CachedOrderAggregationService>();  // gRPC + Redis

var app = builder.Build();

ResiliencePolicies.Logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Resilience");

app.UseCorrelationId();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseAuthentication(); 
app.UseAuthorization();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();