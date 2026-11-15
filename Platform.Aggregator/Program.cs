using System.Text.Json.Serialization;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Metrics;
using Platform.Aggregator.Services;
using Platform.Aggregator.HealthChecks;
using Platform.ServiceDefaults;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = AuthSchemes.OwnJwtParameters(builder.Configuration));
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<Platform.Aggregator.Clients.BearerForwardingHandler>();

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
    .AddHttpMessageHandler<BearerForwardingHandler>();
builder.Services.AddHttpClient<CatalogClient>(c => c.BaseAddress = new("https+http://catalog-api"))
    .AddResilience("catalog-api", ResiliencePolicies.Critical)
    .AddHttpMessageHandler<BearerForwardingHandler>();
builder.Services.AddHttpClient<ReviewsClient>(c => c.BaseAddress = new("https+http://reviews-api"))
    .AddResilience("reviews-api", ResiliencePolicies.NonCritical)
    .AddHttpMessageHandler<BearerForwardingHandler>();

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