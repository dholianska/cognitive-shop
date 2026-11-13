using Platform.EventBus;

namespace Reviews.WebApi.IntegrationEvents;

// Контракт події Каталогу (exchange catalog-events, routing key product.renamed). Власна копія.
public sealed record ProductRenamedEvent(int ProductId, string OldName, string NewName, decimal Price)
    : IntegrationEvent;