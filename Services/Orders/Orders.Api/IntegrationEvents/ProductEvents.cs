using Platform.EventBus;

namespace Orders.Api.IntegrationEvents;

// Контракти подій Каталогу (exchange catalog-events). Копія: сервіси залежать від формату JSON, а не від спільної збірки.

public sealed record ProductCreatedEvent(int ProductId, string Name, string Sku, decimal Price, int AvailableStock)
    : IntegrationEvent;

public sealed record ProductPriceChangedEvent(int ProductId, string ProductName, decimal OldPrice, decimal NewPrice)
    : IntegrationEvent;

public sealed record ProductRenamedEvent(int ProductId, string OldName, string NewName, decimal Price)
    : IntegrationEvent;

public sealed record ProductDeletedEvent(int ProductId, string ProductName) : IntegrationEvent;