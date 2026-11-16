using Reviews.Application;
using Reviews.Application.Common.Mappings;
using Reviews.Infrastructure;
using Reviews.WebApi;
using Reviews.WebApi.GrpcServices;
using Platform.ServiceDefaults;
using Reviews.Application.Caching;
using Reviews.WebApi.Metrics;
using Reviews.WebApi.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Reviews.WebApi.IntegrationEvents;
using Platform.EventBus;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Лаб. 09, Ч.2: приймаємо і власний JWT (Ч.1), і токени IdentityServer (Ч.2); схему обирає політика-схема за issuer
builder.Services.AddAuthentication(o =>
{
    o.DefaultScheme = AuthSchemes.Multi;
    o.DefaultChallengeScheme = AuthSchemes.Multi;
})
.AddPolicyScheme(AuthSchemes.Multi, "Own JWT or IdentityServer",
    o => o.ForwardDefaultSelector = ctx => AuthSchemes.SelectByIssuer(ctx, builder.Configuration))
.AddJwtBearer(AuthSchemes.OwnJwt,
    o => o.TokenValidationParameters = AuthSchemes.OwnJwtParameters(builder.Configuration))
.AddJwtBearer(AuthSchemes.IdentityServer, o =>
{
    o.Authority = builder.Configuration["IdentityServer:Authority"];   // ключі підпису з /.well-known/openid-configuration
    o.Audience = "reviews_api";                                        // ApiResource цього сервісу
    o.RequireHttpsMetadata = false;                                    // розробка: IdentityServer на HTTP
});

// Scope-based політики: дозвіл дає scope з токена IdentityServer або токен власного Auth Service
var ownIssuer = builder.Configuration["Jwt:Issuer"];
builder.Services.AddAuthorizationBuilder()
        .AddPolicy("reviews.read", p => p.RequireAssertion(c => c.User.HasScopeOrOwnJwt("reviews.read", ownIssuer)))
    .AddPolicy("reviews.write", p => p.RequireAssertion(c => c.User.HasScopeOrOwnJwt("reviews.write", ownIssuer)));

builder.AddRabbitMQClient("messaging", s => s.DisableTracing = true);   // IConnection (singleton) + health check; трасування брокера робимо вручну (Частина 4)
builder.AddRedisDistributedCache("redis", s => s.DisableHealthChecks = true);   // власну перевірку (Degraded) реєструємо нижче

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

builder.Services.AddGrpc(o =>
{
    o.EnableDetailedErrors = builder.Environment.IsDevelopment();
    o.Interceptors.Add<GrpcLoggingInterceptor>();
});


builder.Services.AddScoped<ProductRenamedHandler>();
builder.Services.AddHostedService<CatalogEventsConsumer>();
builder.Services.AddDlqDepthMonitor(EventTopology.ReviewsCatalogSync);

// неявний [Required] для non-nullable полів вимкнено: усі правила валідації виконує pipeline MediatR
builder.Services.AddControllers(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

// помилки: стандартний формат ProblemDetails + один глобальний обробник
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

builder.Services.AddHealthChecks()
    .AddCheck<MongoHealthCheck>("reviews-db",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["database", "mongodb", "ready"],
        timeout: TimeSpan.FromSeconds(5))
    .AddCheck<RedisHealthCheck>("redis-cache",
        failureStatus: HealthStatus.Degraded,
        tags: ["cache", "redis", "ready"],
        timeout: TimeSpan.FromSeconds(5));

builder.Services.AddGrpcHealthChecks(o =>
    o.Services.MapService("", r => r.Tags.Contains("ready") && !r.Tags.Contains("cache")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMemoryCache(o =>
{
    o.SizeLimit = 10_000;
    o.CompactionPercentage = 0.20;
    o.ExpirationScanFrequency = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<ICacheMetrics, CacheMetrics>();
builder.Services.AddSingleton<ReviewMetrics>();

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


app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGrpcService<ReviewsGrpcService>();
app.MapGrpcHealthChecksService();
app.Logger.LogInformation("Reviews API started");

app.Run();