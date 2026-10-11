using Serilog;
using Orders.Dal;
using Orders.Dal.Abstractions;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("OrdersDb")
    ?? throw new InvalidOperationException("Connection string 'OrdersDb' is missing");

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// Scoped: один Unit of Work (і одне з'єднання з БД) на HTTP-запит
builder.Services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(connectionString));

builder.Services.AddControllers();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.MapControllers();

app.Logger.LogInformation("Orders API started, connection string loaded");

app.Run();