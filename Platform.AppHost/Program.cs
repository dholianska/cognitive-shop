var builder = DistributedApplication.CreateBuilder(args);

// ѕароль SQL Server: секрет AppHost (user-secrets), не в код≥
var sqlPassword = builder.AddParameter("sql-password", secret: true);

var sql = builder.AddSqlServer("sql", sqlPassword, port: 14330)
    .WithDataVolume();

var ordersDb = sql.AddDatabase("OrdersDb");    // Ћаб. 02
var catalogDb = sql.AddDatabase("CatalogDb");   // Ћаб. 03

var mongo = builder.AddMongoDB("mongo")
    .WithDataVolume();
var reviewsDb = mongo.AddDatabase("ReviewsDb"); // Ћаб. 04

var orders = builder.AddProject<Projects.Orders_Api>("orders-api")
    .WithReference(ordersDb).WaitFor(ordersDb);

var catalog = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb).WaitFor(catalogDb);

var reviews = builder.AddProject<Projects.Reviews_WebApi>("reviews-api")
    .WithReference(reviewsDb).WaitFor(reviewsDb);

var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithReference(orders).WithReference(catalog).WithReference(reviews)
    .WaitFor(orders).WaitFor(catalog).WaitFor(reviews);

builder.AddProject<Projects.Platform_ApiGateway>("gateway")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithReference(orders).WithReference(catalog).WithReference(reviews)
    .WithReference(aggregator)
    .WaitFor(aggregator);

builder.Build().Run();