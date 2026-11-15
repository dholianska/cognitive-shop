namespace Catalog.Bll.Events;

/// <summary>
/// Порт запису подій Каталогу в outbox. Методи лише додають подію в поточний DbContext (без SaveChanges):
/// її збереже той самий SaveChangesAsync, що й бізнес-зміну, а публікує OutboxDispatcher після commit.
/// </summary>
public interface IProductEventsOutbox
{
    Task EnqueueProductCreatedAsync(int id, string name, string sku, decimal price, int stock, CancellationToken ct);
    Task EnqueuePriceChangedAsync(int id, string name, decimal oldPrice, decimal newPrice, CancellationToken ct);
    Task EnqueueRenamedAsync(int id, string oldName, string newName, decimal price, CancellationToken ct);
    Task EnqueueDeletedAsync(int id, string name, CancellationToken ct);
}