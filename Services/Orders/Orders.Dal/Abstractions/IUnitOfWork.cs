using System.Data;

namespace Orders.Dal.Abstractions;

/// <summary>
/// Unit of Work: одне з'єднання та одна транзакція на всі репозиторії бізнес-операції.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    ICustomerRepository Customers { get; }
    IOrderRepository Orders { get; }
    IProductRepository Products { get; }

    Task BeginTransactionAsync(IsolationLevel level = IsolationLevel.ReadCommitted, CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}