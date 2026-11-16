using Orders.Api;
using Orders.Bll.Mapping;
using Orders.Bll.Services;
using Orders.Dal;
using Orders.Dal.Abstractions;
using Orders.Api.GrpcServices;
using Platform.ServiceDefaults;
using Orders.Api.Metrics;
using Orders.Bll.Caching;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Orders.Api.HealthChecks;
using Orders.Api.IntegrationEvents;
using Platform.EventBus;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;

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
    o.Audience = "orders_api";                                         // ApiResource цього сервісу
    o.RequireHttpsMetadata = false;                                    // розробка: IdentityServer на HTTP
});

// Scope-based політики: дозвіл дає scope з токена IdentityServer або токен власного Auth Service
var ownIssuer = builder.Configuration["Jwt:Issuer"];
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("orders.read", p => p.RequireAssertion(c => c.User.HasScopeOrOwnJwt("orders.read", ownIssuer)))
    .AddPolicy("orders.write", p => p.RequireAssertion(c => c.User.HasScopeOrOwnJwt("orders.write", ownIssuer)));

builder.AddRabbitMQClient("messaging", s => s.DisableTracing = true);   // IConnection (singleton) + health check; трасування брокера робимо вручну (Частина 4)
builder.AddRedisDistributedCache("redis", s => s.DisableHealthChecks = true);   // власну перевірку (Degraded) реєструємо нижче

// Рядок підключення: appsettings.json, перекритий user-secrets або змінною оточення
// ConnectionStrings__OrdersDb. Якщо його немає, застосунок не стартує.
var connectionString = builder.Configuration.GetConnectionString("OrdersDb")
    ?? throw new InvalidOperationException("Connection string 'OrdersDb' is missing");

// Scoped: один Unit of Work (і одне з'єднання з БД) на HTTP-запит
builder.Services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(connectionString));

builder.Services.AddAutoMapper(cfg =>
{
    cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"];
    cfg.AddProfile<OrderProfile>();
    cfg.AddProfile<CustomerProfile>();
    cfg.AddProfile<ProductProfile>();
});

builder.Services.AddGrpc(o =>
{
    o.EnableDetailedErrors = builder.Environment.IsDevelopment();
    o.Interceptors.Add<GrpcLoggingInterceptor>();
});

builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();

// обробники подій Каталогу (scope повідомлення) і споживач
builder.Services.AddScoped<ProductCreatedHandler>();
builder.Services.AddScoped<ProductPriceChangedHandler>();
builder.Services.AddScoped<ProductRenamedHandler>();
builder.Services.AddScoped<ProductDeletedHandler>();
builder.Services.AddHostedService<CatalogEventsConsumer>();
builder.Services.AddDlqDepthMonitor(EventTopology.OrdersCatalogSync);

builder.Services.AddControllers();

// помилки: стандартний формат ProblemDetails + один глобальний обробник
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();

var identityServerAuthority = builder.Configuration["IdentityServer:Authority"];
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri($"{identityServerAuthority}/connect/authorize"),
                TokenUrl = new Uri($"{identityServerAuthority}/connect/token"),
                Scopes = new Dictionary<string, string>
                {
                    ["orders.read"] = "Read orders",
                    ["orders.write"] = "Create and change orders"
                }
            }
        }
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oauth2" }
        }] = ["orders.read", "orders.write"]
    });
});

builder.Services.AddMemoryCache(o =>
{
    o.SizeLimit = 10_000;
    o.CompactionPercentage = 0.20;
    o.ExpirationScanFrequency = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<ICacheMetrics, CacheMetrics>();
builder.Services.AddSingleton<ITwoLevelCache, TwoLevelCache>();
builder.Services.AddSingleton<OrderMetrics>();

builder.Services.AddHealthChecks()
    .AddCheck("orders-db",
        new SqlServerHealthCheck(builder.Configuration.GetConnectionString("OrdersDb")!),
        failureStatus: HealthStatus.Unhealthy,
        tags: ["database", "sql", "ready"],
        timeout: TimeSpan.FromSeconds(5))
    .AddCheck<RedisHealthCheck>("redis-cache",
        failureStatus: HealthStatus.Degraded,                 // кеш необов'язковий
        tags: ["cache", "redis", "ready"],
        timeout: TimeSpan.FromSeconds(5));

// стандартний gRPC Health Checking Protocol. Кеш (Degraded) не робить сервіс «NotServing» для Aggregator
builder.Services.AddGrpcHealthChecks(o =>
    o.Services.MapService("", r => r.Tags.Contains("ready") && !r.Tags.Contains("cache")));


var app = builder.Build();


app.UseCorrelationId();
app.MapDefaultEndpoints();

// перевірка мапінгу при старті: одразу видно, якщо в DTO є поле без пари в моделі
if (app.Environment.IsDevelopment())
    app.Services.GetRequiredService<AutoMapper.IConfigurationProvider>().AssertConfigurationIsValid();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(o =>
    {
        o.OAuthClientId("swagger");   // public client: без секрету
        o.OAuthUsePkce();             // PKCE обов'язковий для public client
        o.OAuthScopes("orders.read", "orders.write");
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGrpcService<OrdersGrpcService>();
app.MapGrpcHealthChecksService();

app.Logger.LogInformation("Orders API started");

app.Run();