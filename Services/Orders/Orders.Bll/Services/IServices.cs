using Orders.Bll.Dtos;

namespace Orders.Bll.Services;

public interface ICustomerService
{
    Task<CustomerDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken ct = default);
    Task<CustomerDto> CreateAsync(CreateCustomerDto input, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}

public interface IProductService
{
    Task<ProductDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken ct = default);
}

public interface IOrderService
{
    Task<OrderDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<OrderDto>> GetByCustomerAsync(long customerId, CancellationToken ct = default);

    /// <summary>Транзакційно створює замовлення, деталі й позиції (ціна й назва беруться як знімок).</summary>
    Task<OrderDto> CreateAsync(CreateOrderDto input, CancellationToken ct = default);

    /// <summary>Транзакційно: статус -> StockConfirmed і зменшення залишку товарів.</summary>
    Task ConfirmOrderAsync(long orderId, CancellationToken ct = default);

    Task ChangeStatusAsync(long orderId, string newStatus, CancellationToken ct = default);
    Task DeleteAsync(long orderId, CancellationToken ct = default);
}