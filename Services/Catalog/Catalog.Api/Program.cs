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

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddRedisDistributedCache("redis");   // IDistributedCache поверх Redis; рядок підключення передає Aspire

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

app.Logger.LogInformation("Catalog API started");

app.Run();