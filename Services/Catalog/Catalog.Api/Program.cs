using Catalog.Api;
using Catalog.Bll.Mapping;
using Catalog.Bll.Services;
using Catalog.Bll.Validators;
using Catalog.Dal;
using Catalog.Dal.Abstractions;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Catalog.Api.Metrics;
using Catalog.Bll.Caching;
using Catalog.Api.GrpcServices;
using Platform.ServiceDefaults;
using Catalog.Api.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.EventBus;
using Catalog.Api.IntegrationEvents;
using Catalog.Bll.Events;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddRabbitMQClient("messaging", s => s.DisableTracing = true);   // IConnection (singleton) + health check; трасування брокера робимо вручну (Частина 4)
builder.Services.AddEventBusTopology();
builder.AddRedisDistributedCache("redis", s => s.DisableHealthChecks = true);   // власну перевірку (Degraded) реєструємо нижче

// Рядок підключення: appsettings.json, перекритий user-secrets або змінною оточення
// ConnectionStrings__CatalogDb. Якщо його немає, застосунок не стартує.
var connectionString = builder.Configuration.GetConnectionString("CatalogDb")
    ?? throw new InvalidOperationException("Connection string 'CatalogDb' is missing");

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));

// Scoped: один Unit of Work (і один DbContext) на HTTP-запит
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddAutoMapper(cfg =>
{
    cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"];
    cfg.AddProfile<ProductProfile>();
    cfg.AddProfile<CategoryProfile>();
});

builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();

builder.Services.AddGrpc(o =>
{
    o.EnableDetailedErrors = builder.Environment.IsDevelopment();
    o.Interceptors.Add<GrpcLoggingInterceptor>();
    });

// L1: кеш у пам'яті з обмеженням розміру. Кожен запис має Size = 1, тож максимум 10 000 записів
builder.Services.AddMemoryCache(o =>
{
    o.SizeLimit = 10_000;
    o.CompactionPercentage = 0.20;                        // при переповненні витісняємо 20 % записів
    o.ExpirationScanFrequency = TimeSpan.FromSeconds(30); // як часто чистити прострочені
});

builder.Services.AddSingleton<ICacheMetrics, CacheMetrics>();
builder.Services.AddSingleton<ITwoLevelCache, TwoLevelCache>();
builder.Services.AddSingleton<CatalogMetrics>();

// валідатори (FluentValidation) реєструються з збірки й викликаються в контролерах явно
builder.Services.AddValidatorsFromAssemblyContaining<CreateProductDtoValidator>();

// DataAnnotations лишається лише для простих правил, прописаних атрибутами;
// неявний [Required] для non-nullable полів вимкнено, щоб правила не виконувались у двох місцях
builder.Services.AddControllers(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

// помилки: стандартний формат ProblemDetails + один глобальний обробник
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddHealthChecks()
    .AddCheck("catalog-db",
        new SqlServerHealthCheck(builder.Configuration.GetConnectionString("CatalogDb")!),
        failureStatus: HealthStatus.Unhealthy,
        tags: ["database", "sql", "ready"],
        timeout: TimeSpan.FromSeconds(5))
    .AddCheck<RedisHealthCheck>("redis-cache",
        failureStatus: HealthStatus.Degraded,
        tags: ["cache", "redis", "ready"],
        timeout: TimeSpan.FromSeconds(5));

builder.Services.AddGrpcHealthChecks(o =>
    o.Services.MapService("", r => r.Tags.Contains("ready") && !r.Tags.Contains("cache")));

builder.Services.AddEventPublisher();
builder.Services.AddSingleton<IProductEventsPublisher, ProductEventsPublisher>();

var app = builder.Build();
app.UseCorrelationId();
app.MapDefaultEndpoints();

// перевірка мапінгу при старті: одразу видно, якщо в DTO є поле без пари в сутності
if (app.Environment.IsDevelopment())
    app.Services.GetRequiredService<AutoMapper.IConfigurationProvider>().AssertConfigurationIsValid();

// міграції + програмний сідінг. Сідінг ідемпотентний (повторний запуск не створює дублів)
// і вимикається в production
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    if (!app.Environment.IsProduction())
        await DbInitializer.SeedAsync(db);
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapGrpcService<CatalogGrpcService>();
app.MapGrpcHealthChecksService();

app.Logger.LogInformation("Catalog API started");

app.Run();