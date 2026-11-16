var builder = DistributedApplication.CreateBuilder(args);

var sqlPassword = builder.AddParameter("sql-password", secret: true);
var jwtSecret = builder.AddParameter("jwt-secret", secret: true);    // Лаб. 09: ключ підпису власного JWT

var sql = builder.AddSqlServer("sql", sqlPassword, port: 14330)
    .WithDataVolume();

var ordersDb = sql.AddDatabase("OrdersDb");    // Лаб. 02
var catalogDb = sql.AddDatabase("CatalogDb");   // Лаб. 03
var identityDb = sql.AddDatabase("IdentityDb");  // Лаб. 09, Ч.1: окрема база Auth Service

var mongo = builder.AddMongoDB("mongo")
    .WithDataVolume();
var reviewsDb = mongo.AddDatabase("ReviewsDb"); // Лаб. 04

// Лаб. 06: розподілений кеш. Ім'я "redis" = ConnectionStrings:redis у сервісах
var redis = builder.AddRedis("redis")
    .WithDataVolume();

// Лаб. 08: брокер повідомлень. Пароль фіксований секрет (як sql-password), користувач guest
var rabbitPassword = builder.AddParameter("rabbit-password", secret: true);
var rabbit = builder.AddRabbitMQ("messaging", password: rabbitPassword)
    .WithDataVolume()                     // черги й повідомлення переживають перезапуск
    .WithManagementPlugin(port: 15672); // Management UI: http://localhost:15672

// Лаб. 09, Ч.3 (траєкторія Б): Keycloak через Aspire; WithDataVolume зберігає realm і користувачів між перезапусками
// Оголошуємо його вище, щоб мікросервіси могли посилатися на нього через .WithReference(keycloak)
var keycloakAdmin = builder.AddParameter("keycloak-admin-username", "admin");
var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password", "admin123", secret: true);   // лише для розробки
var keycloak = builder.AddKeycloak("keycloak", 8080, keycloakAdmin, keycloakAdminPassword)
    .WithDataVolume();

var orders = builder.AddProject<Projects.Orders_Api>("orders-api")
    .WithReference(ordersDb).WaitFor(ordersDb)
    .WithReference(redis).WaitFor(redis)
    .WithReference(rabbit).WaitFor(rabbit)
    .WithOwnJwt(jwtSecret)
    .WithIdentityServerAuthority()
    .WithReference(keycloak)
    .WithHttpHealthCheck("/health/ready");

var catalog = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb).WaitFor(catalogDb)
    .WithReference(redis).WaitFor(redis)
    .WithReference(rabbit).WaitFor(rabbit)
    .WithOwnJwt(jwtSecret)
    .WithIdentityServerAuthority()
    .WithReference(keycloak)
    .WithHttpHealthCheck("/health/ready");

var reviews = builder.AddProject<Projects.Reviews_WebApi>("reviews-api")
    .WithReference(reviewsDb).WaitFor(reviewsDb)
    .WithReference(redis).WaitFor(redis)
    .WithReference(rabbit).WaitFor(rabbit)
    .WithOwnJwt(jwtSecret)
    .WithIdentityServerAuthority()
    .WithHttpHealthCheck("/health/ready");

// Лаб. 09, Ч.1: Auth Service (ASP.NET Identity + JWT)
var auth = builder.AddProject<Projects.Auth_Api>("auth-api")
    .WithReference(identityDb).WaitFor(identityDb)
    .WithOwnJwt(jwtSecret)
    .WithHttpHealthCheck("/health/ready");

// Лаб. 09, Ч.2: IdentityServer з ОКРЕМИМ SQL Server і базою (ізоляція безпеки)
var identityServerDb = builder.AddSqlServer("identityserver-sql", sqlPassword, port: 14331)
    .WithDataVolume()
    .AddDatabase("IdentityServerDb");

var identityServer = builder.AddProject<Projects.Platform_IdentityServer>("identityserver")
    .WithReference(identityServerDb).WaitFor(identityServerDb)
    .WithHttpEndpoint(port: 5100, name: "public")   // у методичці 5000, але його вже зайняв Gateway
    .WithHttpHealthCheck("/health/ready");

var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithReference(orders).WithReference(catalog).WithReference(reviews)
    .WithReference(redis)
    .WaitFor(orders).WaitFor(catalog).WaitFor(reviews).WaitFor(redis)
    .WithOwnJwt(jwtSecret)
    .WithIdentityServerAuthority()
    .WaitFor(identityServer)
    .WithHttpHealthCheck("/health/ready");

var payments = builder.AddProject<Projects.Payments_Api>("payments-api")
    .WithReference(keycloak)
    .WithOwnJwt(jwtSecret)
    .WithIdentityServerAuthority()
    .WithHttpHealthCheck("/health/ready");

builder.AddProject<Projects.Platform_ApiGateway>("gateway")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithReference(orders).WithReference(catalog).WithReference(reviews)
    .WithReference(aggregator)
    .WithReference(auth).WaitFor(auth)
    .WaitFor(aggregator)
    .WithOwnJwt(jwtSecret)
    .WithIdentityServerAuthority()
    .WithReference(keycloak)
    .WithReference(payments)
    .WithHttpHealthCheck("/health/ready");

builder.Build().Run();