using System.Data;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Orders.Bll.Dtos;
using Orders.Dal.Abstractions;
using Orders.Domain.Exceptions;
using Orders.Domain.Models;

namespace Orders.Bll.Services;

public sealed class OrderService(IUnitOfWork uow, IMapper mapper, ILogger<OrderService> logger) : IOrderService
{
    private static readonly HashSet<string> KnownStatuses = new()
    {
        OrderStatus.Submitted, OrderStatus.AwaitingValidation, OrderStatus.StockConfirmed,
        OrderStatus.Paid, OrderStatus.Shipped, OrderStatus.Cancelled
    };

    public async Task<OrderDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var order = await uow.Orders.GetWithItemsAsync(id, ct)
                    ?? throw new NotFoundException($"Order {id} not found");
        return mapper.Map<OrderDto>(order);
    }

    public async Task<IReadOnlyList<OrderDto>> GetByCustomerAsync(long customerId, CancellationToken ct = default)
    {
        if (await uow.Customers.GetByIdAsync(customerId, ct) is null)
            throw new NotFoundException($"Customer {customerId} not found");

        return mapper.Map<List<OrderDto>>(await uow.Orders.GetByCustomerAsync(customerId, ct));
    }

    /// <summary>
    /// Транзакція зачіпає три таблиці (Orders, OrderDetails, OrderItems): або все, або нічого.
    /// Ціна й назва товару копіюються в позицію як ЗНІМОК (політика дублювання з Лаб. 01).
    /// </summary>
    public async Task<OrderDto> CreateAsync(CreateOrderDto input, CancellationToken ct = default)
    {
        // однакові товари складаємо в одну позицію
        var lines = input.Items
            .GroupBy(i => i.ProductId)
            .Select(g => (ProductId: g.Key, Units: g.Sum(i => i.Units)))
            .ToList();

        long orderId;
        await uow.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            // бізнес-валідація, якій потрібен стан БД
            _ = await uow.Customers.GetByIdAsync(input.CustomerId, ct)
                ?? throw new NotFoundException($"Customer {input.CustomerId} not found");

            var items = new List<OrderItem>();
            foreach (var line in lines)
            {
                var product = await uow.Products.GetByIdAsync(line.ProductId, ct)
                              ?? throw new NotFoundException($"Product {line.ProductId} not found");

                // раннє попередження; остаточне списання залишку (атомарне) відбувається при підтвердженні
                if (product.Stock < line.Units)
                    throw new BusinessConflictException(
                        $"Not enough stock for '{product.Name}': requested {line.Units}, available {product.Stock}");

                items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,    // знімок
                    UnitPrice = product.Price,     // знімок
                    Discount = 0,
                    Units = line.Units
                });
            }

            orderId = await uow.Orders.AddAsync(
                new Order { CustomerId = input.CustomerId, Status = OrderStatus.Submitted }, ct);
            await uow.Orders.AddDetailsAsync(orderId, input.DeliveryEmail.Trim(), input.Comment, ct);

            foreach (var item in items)
            {
                item.OrderId = orderId;
                await uow.Orders.AddItemAsync(item, ct);
            }

            await uow.CommitAsync(ct);
        }
        catch
        {
            await uow.RollbackAsync(ct);   // жодної частково створеної позиції в базі не лишається
            throw;
        }

        logger.LogInformation("Order {OrderId} created for customer {CustomerId} with {ItemCount} items",
            orderId, input.CustomerId, lines.Count);

        return await GetByIdAsync(orderId, ct);
    }

    /// <summary>
    /// Підтвердження: зміна статусу (процедура usp_ConfirmOrder) і зменшення залишку товарів
    /// в ОДНІЙ транзакції. Нестача товару в будь-якій позиції відкочує і статус.
    /// </summary>
    public async Task ConfirmOrderAsync(long orderId, CancellationToken ct = default)
    {
        await uow.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var order = await uow.Orders.GetWithItemsAsync(orderId, ct)
                        ?? throw new NotFoundException($"Order {orderId} not found");

            if (order.Items.Count == 0)
                throw new BusinessConflictException($"Order {orderId} has no items");

            // -> StockConfirmed; допустимість переходу перевіряє процедура з Лаб. 01
            await uow.Orders.ConfirmAsync(orderId, ct);

            // брак товару -> BusinessConflictException -> відкат
            foreach (var item in order.Items)
                await uow.Products.DecreaseStockAsync(item.ProductId, item.Units, ct);

            await uow.CommitAsync(ct);
        }
        catch
        {
            await uow.RollbackAsync(ct);   // статус повертається назад
            throw;
        }

        logger.LogInformation("Order {OrderId} confirmed", orderId);
    }

    public async Task ChangeStatusAsync(long orderId, string newStatus, CancellationToken ct = default)
    {
        if (!KnownStatuses.Contains(newStatus))
            throw new ValidationException($"Unknown status '{newStatus}'");

        // StockConfirmed ставить лише підтвердження, бо воно ще й списує залишок
        if (newStatus == OrderStatus.StockConfirmed)
            throw new ValidationException("Use POST /api/orders/{id}/confirm to confirm an order");

        await uow.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var order = await uow.Orders.GetWithItemsAsync(orderId, ct)
                        ?? throw new NotFoundException($"Order {orderId} not found");

            // скасування після підтвердження повертає залишок, який зменшило підтвердження
            if (newStatus == OrderStatus.Cancelled && order.Status == OrderStatus.StockConfirmed)
                foreach (var item in order.Items)
                    await uow.Products.IncreaseStockAsync(item.ProductId, item.Units, ct);

            // допустимість переходу перевіряє процедура; при відмові відкотиться і повернення залишку
            await uow.Orders.ChangeStatusAsync(orderId, newStatus, ct);

            await uow.CommitAsync(ct);
        }
        catch
        {
            await uow.RollbackAsync(ct);
            throw;
        }

        logger.LogInformation("Order {OrderId} status changed to {Status}", orderId, newStatus);
    }

    public async Task DeleteAsync(long orderId, CancellationToken ct = default)
    {
        var order = await uow.Orders.GetByIdAsync(orderId, ct)
                    ?? throw new NotFoundException($"Order {orderId} not found");

        if (order.Status is not (OrderStatus.Submitted or OrderStatus.Cancelled))
            throw new BusinessConflictException("Only submitted or cancelled orders can be deleted");

        await uow.Orders.DeleteAsync(orderId, ct);   // м'яке видалення (IsDeleted = 1)
    }
}