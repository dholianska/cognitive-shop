var builder = DistributedApplication.CreateBuilder(args);

// Пароль адміністратора SQL Server береться з секретів AppHost, у репозиторії його немає.
var sqlPassword = builder.AddParameter("sql-password", secret: true);

// SQL Server у контейнері. Фіксований порт 14330 дозволяє підключитися з SSMS/Visual Studio,
// щоб застосувати скрипти з Лаб. 01. WithDataVolume зберігає дані між перезапусками.
var sql = builder.AddSqlServer("sql", sqlPassword, port: 14330)
    .WithDataVolume();

// Ім'я ресурсу "OrdersDb" збігається з ім'ям рядка підключення в API
// (GetConnectionString("OrdersDb")), тому Aspire передасть його в API автоматично.
var ordersDb = sql.AddDatabase("OrdersDb");

builder.AddProject<Projects.Orders_Api>("orders-api")
    .WithReference(ordersDb)
    .WaitFor(ordersDb);

builder.Build().Run();