using Reviews.Application;
using Reviews.Application.Common.Mappings;
using Reviews.Infrastructure;
using Reviews.WebApi;
using Reviews.WebApi.GrpcServices;
using Platform.ServiceDefaults;
using Reviews.Application.Caching;
using Reviews.WebApi.Metrics;
using Reviews.WebApi.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Reviews.WebApi.IntegrationEvents;
using Platform.EventBus;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddRabbitMQClient("messaging", s => s.DisableTracing = true);   // IConnection (singleton) + health check; трасування брокера робимо вручну (Частина 4)
builder.AddRedisDistributedCache("redis", s => s.DisableHealthChecks = true);   // власну перевірку (Degraded) реєструємо нижче

// Aspire передає ConnectionStrings:ReviewsDb; кладемо його туди, звідки його читає Лаб. 04
var mongoCs = builder.Configuration.GetConnectionString("ReviewsDb");
if (!string.IsNullOrWhiteSpace(mongoCs))
    builder.Configuration["MongoDb:ConnectionString"] = mongoCs;

// кожен шар надає власний метод розширення
builder.Services
    .AddApplication(builder.Configuration)       // MediatR, behaviors, валідатори
    .AddInfrastructure(builder.Configuration);   // MongoDB: контекст, мапінг, репозиторії, індекси, сідінг

builder.Services.AddAutoMapper(cfg =>
{
    cfg.LicenseKey = builder.Configuration["LuckyPenny:LicenseKey"];   // необов'язково, як і для MediatR
    cfg.AddMaps(typeof(ReviewProfile).Assembly);
});

builder.Services.AddGrpc(o =>
{
    o.EnableDetailedErrors = builder.Environment.IsDevelopment();
    o.Interceptors.Add<GrpcLoggingInterceptor>();
});


builder.Services.AddScoped<ProductRenamedHandler>();
builder.Services.AddHostedService<CatalogEventsConsumer>();
builder.Services.AddDlqDepthMonitor(EventTopology.ReviewsCatalogSync);

// неявний [Required] для non-nullable полів вимкнено: усі правила валідації виконує pipeline MediatR
builder.Services.AddControllers(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

// помилки: стандартний формат ProblemDetails + один глобальний обробник
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

builder.Services.AddHealthChecks()
    .AddCheck<MongoHealthCheck>("reviews-db",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["database", "mongodb", "ready"],
        timeout: TimeSpan.FromSeconds(5))
    .AddCheck<RedisHealthCheck>("redis-cache",
        failureStatus: HealthStatus.Degraded,
        tags: ["cache", "redis", "ready"],
        timeout: TimeSpan.FromSeconds(5));

builder.Services.AddGrpcHealthChecks(o =>
    o.Services.MapService("", r => r.Tags.Contains("ready") && !r.Tags.Contains("cache")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMemoryCache(o =>
{
    o.SizeLimit = 10_000;
    o.CompactionPercentage = 0.20;
    o.ExpirationScanFrequency = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<ICacheMetrics, CacheMetrics>();
builder.Services.AddSingleton<ReviewMetrics>();

var app = builder.Build();
app.UseCorrelationId();
app.MapDefaultEndpoints();

// перевірка мапінгу при старті: одразу видно, якщо в DTO є поле без пари в сутності
if (app.Environment.IsDevelopment())
    app.Services.GetRequiredService<AutoMapper.IConfigurationProvider>().AssertConfigurationIsValid();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapGrpcService<ReviewsGrpcService>();
app.MapGrpcHealthChecksService();
app.Logger.LogInformation("Reviews API started");

app.Run();