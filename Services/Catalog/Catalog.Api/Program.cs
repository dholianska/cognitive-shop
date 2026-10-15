using Catalog.Dal;
using Catalog.Dal.Abstractions;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// –€док п≥дключенн€: appsettings.json, перекритий user-secrets або зм≥нною оточенн€
// ConnectionStrings__CatalogDb. якщо його немаЇ, застосунок не стартуЇ.
var connectionString = builder.Configuration.GetConnectionString("CatalogDb")
    ?? throw new InvalidOperationException("Connection string 'CatalogDb' is missing");

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command",
                           LogEventLevel.Information)   // SQL-запити EF Core
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));

// Scoped: один Unit of Work (≥ один DbContext) на HTTP-запит
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddControllers();

var app = builder.Build();

app.UseSerilogRequestLogging(o => o.EnrichDiagnosticContext = (diag, http) =>
    diag.Set("CorrelationId", http.TraceIdentifier));

app.MapControllers();

app.Logger.LogInformation("Catalog API started");

app.Run();