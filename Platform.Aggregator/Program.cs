using System.Text.Json.Serialization;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// camelCase за замовчуванням; null-значення не серіалізуються.
// Для контролерів потрібен саме AddJsonOptions: ConfigureHttpJsonOptions діє лише на minimal API.
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);
builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

builder.Services.AddOpenApi();

// Typed HttpClients: адреса за логічним іменем, її підставляє service discovery
builder.Services.AddHttpClient<OrdersClient>(c => c.BaseAddress = new("https+http://orders-api"));
builder.Services.AddHttpClient<CatalogClient>(c => c.BaseAddress = new("https+http://catalog-api"));
builder.Services.AddHttpClient<ReviewsClient>(c => c.BaseAddress = new("https+http://reviews-api"));

builder.Services.AddScoped<OrderAggregationService>();

var app = builder.Build();

app.UseCorrelationId();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();