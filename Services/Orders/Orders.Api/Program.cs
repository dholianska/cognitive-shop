using Serilog;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("OrdersDb")
    ?? throw new InvalidOperationException("Connection string 'OrdersDb' is missing");

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddControllers();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.MapControllers();

app.Logger.LogInformation("Orders API started, connection string loaded");

app.Run();