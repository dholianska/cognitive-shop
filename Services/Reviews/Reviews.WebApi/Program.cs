using Reviews.Application;
using Reviews.Application.Common.Mappings;
using Reviews.Infrastructure;
using Reviews.WebApi;
using Reviews.WebApi.Health;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

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

// неявний [Required] для non-nullable полів вимкнено: усі правила валідації виконує pipeline MediatR
builder.Services.AddControllers(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

// помилки: стандартний формат ProblemDetails + один глобальний обробник
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

builder.Services.AddHealthChecks().AddCheck<MongoHealthCheck>("mongodb");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
app.Logger.LogInformation("Reviews API started");

app.Run();