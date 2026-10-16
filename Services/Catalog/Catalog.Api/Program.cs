using Catalog.Api;
using Catalog.Bll.Mapping;
using Catalog.Bll.Services;
using Catalog.Bll.Validators;
using Catalog.Dal;
using Catalog.Dal.Abstractions;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Рядок підключення: appsettings.json, перекритий user-secrets або змінною оточення
// ConnectionStrings__CatalogDb. Якщо його немає, застосунок не стартує.
var connectionString = builder.Configuration.GetConnectionString("CatalogDb")
    ?? throw new InvalidOperationException("Connection string 'CatalogDb' is missing");

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command",
                           LogEventLevel.Information)   // SQL-запити EF Core
    .Enrich.FromLogContext()
    .WriteTo.Console());

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
app.UseSerilogRequestLogging(o => o.EnrichDiagnosticContext = (diag, http) =>
    diag.Set("CorrelationId", http.TraceIdentifier));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Logger.LogInformation("Catalog API started");

app.Run();