var builder = DistributedApplication.CreateBuilder(args);

var sqlPassword = builder.AddParameter("sql-password", secret: true);

var sql = builder.AddSqlServer("sql", sqlPassword, port: 14330)
    .WithDataVolume();

var ordersDb = sql.AddDatabase("OrdersDb");    // Лаб. 02
var catalogDb = sql.AddDatabase("CatalogDb");   // Лаб. 03

var mongo = builder.AddMongoDB("mongo")
    .WithDataVolume();
var reviewsDb = mongo.AddDatabase("ReviewsDb"); // Лаб. 04

// Лаб. 06: розподілений кеш. Ім'я "redis" = ConnectionStrings:redis у сервісах
var redis = builder.AddRedis("redis")
    .WithDataVolume();

var orders = builder.AddProject<Projects.Orders_Api>("orders-api")
    .WithReference(ordersDb).WaitFor(ordersDb)
    .WithReference(redis).WaitFor(redis)
    .WithHttpHealthCheck("/health/ready");

var catalog = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb).WaitFor(catalogDb)
    .WithReference(redis).WaitFor(redis)
    .WithHttpHealthCheck("/health/ready");

var reviews = builder.AddProject<Projects.Reviews_WebApi>("reviews-api")
    .WithReference(reviewsDb).WaitFor(reviewsDb)
    .WithReference(redis).WaitFor(redis)
    .WithHttpHealthCheck("/health/ready");

var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithReference(orders).WithReference(catalog).WithReference(reviews)
    .WithReference(redis)
    .WaitFor(orders).WaitFor(catalog).WaitFor(reviews).WaitFor(redis)
    .WithHttpHealthCheck("/health/ready");

builder.AddProject<Projects.Platform_ApiGateway>("gateway")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithReference(orders).WithReference(catalog).WithReference(reviews)
    .WithReference(aggregator)
    .WaitFor(aggregator)
    .WithHttpHealthCheck("/health/ready");

builder.Build().Run();