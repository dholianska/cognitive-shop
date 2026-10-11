using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Orders.Dal.Abstractions;
using Orders.Domain.Models;

namespace Orders.Dal.Repositories;

public sealed class OrderRepository : BaseDapperRepository<Order>, IOrderRepository
{
    public OrderRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction, "dbo.Orders") { }

    protected override string InsertSql =>
        "INSERT dbo.Orders (CustomerId, Status) OUTPUT INSERTED.Id VALUES (@CustomerId, @Status)";

    public async Task<Order?> GetWithItemsAsync(long orderId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT o.Id, o.CustomerId, o.Status, o.OrderDate, d.DeliveryEmail, d.Comment,
                   i.OrderId, i.ProductId, i.ProductName, i.UnitPrice, i.Discount, i.Units
            FROM dbo.Orders o
            LEFT JOIN dbo.OrderDetails d ON d.OrderId = o.Id
            LEFT JOIN dbo.OrderItems i ON i.OrderId = o.Id
            WHERE o.Id = @OrderId AND o.IsDeleted = 0
            ORDER BY i.ProductId";

        await EnsureOpenAsync(ct);

        Order? result = null;
        await Connection.QueryAsync<Order, OrderItem?, Order>(
            new CommandDefinition(sql, new { OrderId = orderId }, Transaction, cancellationToken: ct),
            (order, item) =>            // викликається для кожного рядка JOIN: Order + одна позиція
            {
                result ??= order;
                if (item is not null) result.Items.Add(item);
                return result;
            },
            splitOn: "OrderId");        // з цього стовпця починається OrderItem

        return result;
    }

    public async Task<IReadOnlyList<Order>> GetByCustomerAsync(long customerId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, CustomerId, Status, OrderDate
            FROM dbo.Orders
            WHERE CustomerId = @CustomerId AND IsDeleted = 0
            ORDER BY OrderDate DESC";

        await EnsureOpenAsync(ct);
        var rows = await Connection.QueryAsync<Order>(
            new CommandDefinition(sql, new { CustomerId = customerId }, Transaction, cancellationToken: ct));
        return rows.AsList();
    }

    public Task AddDetailsAsync(long orderId, string deliveryEmail, string? comment, CancellationToken ct = default)
    => GuardAsync(async () =>
    {
        const string sql = @"
                INSERT dbo.OrderDetails (OrderId, DeliveryEmail, Comment)
                VALUES (@OrderId, @DeliveryEmail, @Comment)";

        await EnsureOpenAsync(ct);
        await Connection.ExecuteAsync(new CommandDefinition(
            sql, new { OrderId = orderId, DeliveryEmail = deliveryEmail, Comment = comment },
            Transaction, cancellationToken: ct));
    });

    public Task AddItemAsync(OrderItem item, CancellationToken ct = default)
        => GuardAsync(async () =>
        {
            const string sql = @"
                INSERT dbo.OrderItems (OrderId, ProductId, ProductName, UnitPrice, Discount, Units)
                VALUES (@OrderId, @ProductId, @ProductName, @UnitPrice, @Discount, @Units)";

            await EnsureOpenAsync(ct);
            await Connection.ExecuteAsync(new CommandDefinition(sql, item, Transaction, cancellationToken: ct));
        });

    /// <summary>Процедура з Лаб. 01: допустимість переходу перевіряє база.</summary>
    public Task ConfirmAsync(long orderId, CancellationToken ct = default)
        => GuardAsync(async () =>
        {
            await EnsureOpenAsync(ct);
            await Connection.ExecuteAsync(new CommandDefinition(
                "dbo.usp_ConfirmOrder", new { OrderId = orderId }, Transaction,
                commandType: CommandType.StoredProcedure, cancellationToken: ct));
        });

    public Task ChangeStatusAsync(long orderId, string newStatus, CancellationToken ct = default)
        => GuardAsync(async () =>
        {
            await EnsureOpenAsync(ct);
            await Connection.ExecuteAsync(new CommandDefinition(
                "dbo.usp_ChangeOrderStatus", new { OrderId = orderId, NewStatus = newStatus }, Transaction,
                commandType: CommandType.StoredProcedure, cancellationToken: ct));
        });
}