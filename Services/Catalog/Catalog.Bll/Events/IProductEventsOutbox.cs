namespace Catalog.Bll.Events;

/// <summary>Порт публікації подій Каталогу. Викликається лише після успішного збереження змін у БД.</summary>
public interface IProductEventsPublisher
{
    Task ProductCreatedAsync(int id, string name, string sku, decimal price, int stock, CancellationToken ct);
    Task PriceChangedAsync(int id, string name, decimal oldPrice, decimal newPrice, CancellationToken ct);
    Task RenamedAsync(int id, string oldName, string newName, decimal price, CancellationToken ct);
    Task DeletedAsync(int id, string name, CancellationToken ct);
}