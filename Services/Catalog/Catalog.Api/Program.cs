using Serilog;

var builder = WebApplication.CreateBuilder(args);

// –€док п≥дключенн€: appsettings.json, перекритий user-secrets або зм≥нною оточенн€
// ConnectionStrings__CatalogDb. якщо його немаЇ, застосунок не стартуЇ.
var connectionString = builder.Configuration.GetConnectionString("CatalogDb")
    ?? throw new InvalidOperationException("Connection string 'CatalogDb' is missing");

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddControllers();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.MapControllers();

app.Logger.LogInformation("Catalog API started, connection string loaded");

app.Run();