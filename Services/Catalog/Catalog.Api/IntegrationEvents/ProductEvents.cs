using Platform.EventBus;

namespace Catalog.Api.IntegrationEvents;

/// <summary>Створено товар. Замовлення додають його в локальну копію Products.</summary>
public sealed record ProductCreatedEvent(int ProductId, string Name, string Sku, decimal Price, int AvailableStock)
    : IntegrationEvent;

/// <summary>Змінено ціну. Містить назву, щоб споживач міг оновити копію без звернення до Каталогу.</summary>
public sealed record ProductPriceChangedEvent(int ProductId, string ProductName, decimal OldPrice, decimal NewPrice)
    : IntegrationEvent;

/// <summary>Перейменовано товар. Містить актуальну ціну для повного зрізу.</summary>
public sealed record ProductRenamedEvent(int ProductId, string OldName, string NewName, decimal Price)
    : IntegrationEvent;

/// <summary>Видалено товар.</summary>
public sealed record ProductDeletedEvent(int ProductId, string ProductName) : IntegrationEvent;