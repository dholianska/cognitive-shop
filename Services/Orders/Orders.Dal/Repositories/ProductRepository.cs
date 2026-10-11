using Dapper;
using Microsoft.Data.SqlClient;
using Orders.Dal.Abstractions;
using Orders.Domain.Exceptions;
using Orders.Domain.Models;

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
}