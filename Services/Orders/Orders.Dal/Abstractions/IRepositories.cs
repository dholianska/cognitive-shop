using Orders.Domain.Models;

namespace Orders.Dal.Abstractions;

/// <summary>
/// Репозиторій на чистому ADO.NET. Свідомо НЕ успадковує IGenericRepository
/// (демонстрація роботи без сторонніх бібліотек).
/// </summary>
public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Створює клієнта (процедура usp_CreateCustomer) і повертає його Id.</summary>
    Task<long> AddAsync(Customer customer, CancellationToken ct = default);

    /// <summary>М'яке видалення клієнта (процедура usp_SoftDeleteCustomer).</summary>
    Task SoftDeleteAsync(long id, CancellationToken ct = default);
}

public interface IOrderRepository : IGenericRepository<Order>
{
    /// <summary>Замовлення з деталями (OrderDetails) і позиціями (OrderItems) одним запитом.</summary>
    Task<Order?> GetWithItemsAsync(long orderId, CancellationToken ct = default);

    Task<IReadOnlyList<Order>> GetByCustomerAsync(long customerId, CancellationToken ct = default);

    Task AddDetailsAsync(long orderId, string deliveryEmail, string? comment, CancellationToken ct = default);
    Task AddItemAsync(OrderItem item, CancellationToken ct = default);

    /// <summary>Процедура usp_ConfirmOrder (Submitted/AwaitingValidation -> StockConfirmed).</summary>
    Task ConfirmAsync(long orderId, CancellationToken ct = default);

    /// <summary>Процедура usp_ChangeOrderStatus: допустимість переходу перевіряє база.</summary>
    Task ChangeStatusAsync(long orderId, string newStatus, CancellationToken ct = default);
}

public interface IProductRepository : IGenericRepository<Product>
{
    /// <summary>Зменшує залишок. Якщо товару не вистачає, кидає BusinessConflictException.</summary>
    Task DecreaseStockAsync(long productId, int quantity, CancellationToken ct = default);
    /// <summary>Повертає залишок (скасування підтвердженого замовлення).</summary>
    Task IncreaseStockAsync(long productId, int quantity, CancellationToken ct = default);
}