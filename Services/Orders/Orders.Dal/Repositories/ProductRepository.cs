using Dapper;
using Microsoft.Data.SqlClient;
using Orders.Dal.Abstractions;
using Orders.Domain.Exceptions;
using Orders.Domain.Models;
using System.Data;

namespace Orders.Dal.Repositories;

public sealed class ProductRepository : BaseDapperRepository<Product>, IProductRepository
{
    public ProductRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction, "dbo.Products") { }

    // Id не IDENTITY: це Id з БД Каталогу (локальна копія довідника)
    protected override string InsertSql =>
        "INSERT dbo.Products (Id, Name, Price, Stock) OUTPUT INSERTED.Id VALUES (@Id, @Name, @Price, @Stock)";

    public async Task DecreaseStockAsync(long productId, int quantity, CancellationToken ct = default)
    {
        if (quantity <= 0)
            throw new ValidationException("Quantity must be positive");

        await EnsureOpenAsync(ct);

        // умова Stock >= @Quantity в самому UPDATE: перевірка й зміна атомарні
        const string updateSql = @"
            UPDATE dbo.Products
            SET Stock = Stock - @Quantity, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @ProductId AND IsDeleted = 0 AND Stock >= @Quantity";

        var rows = await Connection.ExecuteAsync(new CommandDefinition(
            updateSql, new { ProductId = productId, Quantity = quantity }, Transaction, cancellationToken: ct));
        if (rows > 0) return;

        // 0 рядків: або товару немає, або не вистачає залишку
        const string existsSql = "SELECT COUNT(1) FROM dbo.Products WHERE Id = @ProductId AND IsDeleted = 0";
        var exists = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(
            existsSql, new { ProductId = productId }, Transaction, cancellationToken: ct));

        if (exists == 0)
            throw new NotFoundException($"Product {productId} not found");
        throw new BusinessConflictException($"Not enough stock for product {productId}");
    }

    public async Task IncreaseStockAsync(long productId, int quantity, CancellationToken ct = default)
    {
        if (quantity <= 0)
            throw new ValidationException("Quantity must be positive");

        await EnsureOpenAsync(ct);

        const string sql = @"
            UPDATE dbo.Products
            SET Stock = Stock + @Quantity, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @ProductId AND IsDeleted = 0";

        var rows = await Connection.ExecuteAsync(new CommandDefinition(
            sql, new { ProductId = productId, Quantity = quantity }, Transaction, cancellationToken: ct));

        if (rows == 0)
            throw new NotFoundException($"Product {productId} not found");
    }

    public Task<bool> UpsertSnapshotAsync(long productId, string name, decimal price, int initialStock,
    DateTime occurredAt, CancellationToken ct = default)
    => GuardValueAsync(async () =>
    {
        await EnsureOpenAsync(ct);

        // 1) UPDATE лише якщо подія новіша за вже застосовану; 2) якщо товару ще немає, INSERT.
        // Stock чіпаємо тільки при створенні: залишок у Замовленнях веде сам сервіс Замовлень
        const string sql = @"
            DECLARE @applied INT = 0;

            UPDATE dbo.Products
            SET Name = @Name, Price = @Price, IsDeleted = 0, SnapshotAt = @At, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND (SnapshotAt IS NULL OR SnapshotAt < @At);
            SET @applied = @@ROWCOUNT;

            IF @applied = 0 AND NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Id = @Id)
            BEGIN
                INSERT dbo.Products (Id, Name, Price, Stock, SnapshotAt)
                VALUES (@Id, @Name, @Price, @Stock, @At);
                SET @applied = 1;
            END

            SELECT @applied;";

        var parameters = new DynamicParameters();
        parameters.Add("Id", productId);
        parameters.Add("Name", name);
        parameters.Add("Price", price);
        parameters.Add("Stock", initialStock);
        parameters.Add("At", occurredAt, DbType.DateTime2);

        var applied = await Connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, parameters, Transaction, cancellationToken: ct));
        return applied > 0;
    });

    public Task<bool> MarkDeletedAsync(long productId, DateTime occurredAt, CancellationToken ct = default)
        => GuardValueAsync(async () =>
        {
            await EnsureOpenAsync(ct);

            const string sql = @"
            UPDATE dbo.Products
            SET IsDeleted = 1, SnapshotAt = @At, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND (SnapshotAt IS NULL OR SnapshotAt < @At)";

            var parameters = new DynamicParameters();
            parameters.Add("Id", productId);
            parameters.Add("At", occurredAt, DbType.DateTime2);

            var rows = await Connection.ExecuteAsync(
                new CommandDefinition(sql, parameters, Transaction, cancellationToken: ct));
            return rows > 0;
        });
}