using Orders.Api;
using Orders.Bll.Mapping;
using Orders.Bll.Services;
using Orders.Dal;
using Orders.Dal.Abstractions;
using Orders.Api.GrpcServices;
using Platform.ServiceDefaults;
using Orders.Api.Metrics;
using Orders.Bll.Caching;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Orders.Api.HealthChecks;
using Orders.Api.IntegrationEvents;
using Platform.EventBus;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddRabbitMQClient("messaging", s => s.DisableTracing = true);   // IConnection (singleton) + health check; трасування брокера робимо вручну (Частина 4)
builder.AddRedisDistributedCache("redis", s => s.DisableHealthChecks = true);   // власну перевірку (Degraded) реєструємо нижче

// Рядок підключення: appsettings.json, перекритий user-secrets або змінною оточення
// ConnectionStrings__OrdersDb. Якщо його немає, застосунок не стартує.
var connectionString = builder.Configuration.GetConnectionString("OrdersDb")
    ?? throw new InvalidOperationException("Connection string 'OrdersDb' is missing");

// Scoped: один Unit of Work (і одне з'єднання з БД) на HTTP-запит
builder.Services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(connectionString));

builder.Services.AddAutoMapper(cfg =>
{
    cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"];
    cfg.AddProfile<OrderProfile>();
    cfg.AddProfile<CustomerProfile>();
    cfg.AddProfile<ProductProfile>();
});

builder.Services.AddGrpc(o =>
{
    o.EnableDetailedErrors = builder.Environment.IsDevelopment();
    o.Interceptors.Add<GrpcLoggingInterceptor>();
});

builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();

// обробники подій Каталогу (scope повідомлення) і споживач
builder.Services.AddScoped<ProductCreatedHandler>();
builder.Services.AddScoped<ProductPriceChangedHandler>();
builder.Services.AddScoped<ProductRenamedHandler>();
builder.Services.AddScoped<ProductDeletedHandler>();
builder.Services.AddHostedService<CatalogEventsConsumer>();
builder.Services.AddDlqDepthMonitor(EventTopology.OrdersCatalogSync);

builder.Services.AddControllers();

// помилки: стандартний формат ProblemDetails + один глобальний обробник
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMemoryCache(o =>
{
    o.SizeLimit = 10_000;
    o.CompactionPercentage = 0.20;
    o.ExpirationScanFrequency = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<ICacheMetrics, CacheMetrics>();
builder.Services.AddSingleton<ITwoLevelCache, TwoLevelCache>();
builder.Services.AddSingleton<OrderMetrics>();

builder.Services.AddHealthChecks()
    .AddCheck("orders-db",
        new SqlServerHealthCheck(builder.Configuration.GetConnectionString("OrdersDb")!),
        failureStatus: HealthStatus.Unhealthy,
        tags: ["database", "sql", "ready"],
        timeout: TimeSpan.FromSeconds(5))
    .AddCheck<RedisHealthCheck>("redis-cache",
        failureStatus: HealthStatus.Degraded,                 // кеш необов'язковий
        tags: ["cache", "redis", "ready"],
        timeout: TimeSpan.FromSeconds(5));

// стандартний gRPC Health Checking Protocol. Кеш (Degraded) не робить сервіс «NotServing» для Aggregator
builder.Services.AddGrpcHealthChecks(o =>
    o.Services.MapService("", r => r.Tags.Contains("ready") && !r.Tags.Contains("cache")));


var app = builder.Build();


app.UseCorrelationId();
app.MapDefaultEndpoints();

// перевірка мапінгу при старті: одразу видно, якщо в DTO є поле без пари в моделі
if (app.Environment.IsDevelopment())
    app.Services.GetRequiredService<AutoMapper.IConfigurationProvider>().AssertConfigurationIsValid();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapGrpcService<OrdersGrpcService>();
app.MapGrpcHealthChecksService();

app.Logger.LogInformation("Orders API started");

app.Run();