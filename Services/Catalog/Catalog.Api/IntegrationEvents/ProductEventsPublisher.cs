using Catalog.Bll.Events;
using Platform.EventBus;

namespace Catalog.Api.IntegrationEvents;

public sealed class ProductEventsPublisher(IEventPublisher publisher, ILogger<ProductEventsPublisher> logger)
    : IProductEventsPublisher
{
    public Task ProductCreatedAsync(int id, string name, string sku, decimal price, int stock, CancellationToken ct)
        => SafePublishAsync(id,
            new ProductCreatedEvent(id, name, sku, price, stock) { CorrelationId = CorrelationContext.Current },
            RoutingKeys.ProductCreated, ct);

    public Task PriceChangedAsync(int id, string name, decimal oldPrice, decimal newPrice, CancellationToken ct)
        => SafePublishAsync(id,
            new ProductPriceChangedEvent(id, name, oldPrice, newPrice) { CorrelationId = CorrelationContext.Current },
            RoutingKeys.ProductPriceChanged, ct);

    public Task RenamedAsync(int id, string oldName, string newName, decimal price, CancellationToken ct)
        => SafePublishAsync(id,
            new ProductRenamedEvent(id, oldName, newName, price) { CorrelationId = CorrelationContext.Current },
            RoutingKeys.ProductRenamed, ct);

    public Task DeletedAsync(int id, string name, CancellationToken ct)
        => SafePublishAsync(id,
            new ProductDeletedEvent(id, name) { CorrelationId = CorrelationContext.Current },
            RoutingKeys.ProductDeleted, ct);

    private async Task SafePublishAsync<T>(int productId, T @event, string routingKey, CancellationToken ct)
        where T : IntegrationEvent
    {
        try
        {
            await publisher.PublishAsync(@event, Exchanges.Catalog, routingKey, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // зміни в БД уже збережено, тож запит не провалюємо; без outbox така подія втрачається
            logger.LogError(ex,
                "Failed to publish {EventType} {EventId} for product {ProductId}, correlation {CorrelationId}: change is saved, event is lost",
                typeof(T).Name, @event.EventId, productId, @event.CorrelationId);
        }
    }
}