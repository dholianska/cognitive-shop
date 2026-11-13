using System.Diagnostics;
using System.Text.Json;
using Catalog.Bll.Events;
using Catalog.Dal;
using Catalog.Dal.Outbox;
using Platform.EventBus;

namespace Catalog.Api.IntegrationEvents;

/// <summary>
/// Будує події та додає їх в outbox поточного DbContext (той самий, що в UnitOfWork).
/// Окремого SaveChanges тут немає: подія зберігається атомарно з бізнес-зміною.
/// </summary>
public sealed class ProductEventsOutbox(AppDbContext db) : IProductEventsOutbox
{
    public Task EnqueueProductCreatedAsync(int id, string name, string sku, decimal price, int stock, CancellationToken ct)
        => Add(new ProductCreatedEvent(id, name, sku, price, stock) { CorrelationId = CorrelationContext.Current },
            RoutingKeys.ProductCreated);

    public Task EnqueuePriceChangedAsync(int id, string name, decimal oldPrice, decimal newPrice, CancellationToken ct)
        => Add(new ProductPriceChangedEvent(id, name, oldPrice, newPrice) { CorrelationId = CorrelationContext.Current },
            RoutingKeys.ProductPriceChanged);

    public Task EnqueueRenamedAsync(int id, string oldName, string newName, decimal price, CancellationToken ct)
        => Add(new ProductRenamedEvent(id, oldName, newName, price) { CorrelationId = CorrelationContext.Current },
            RoutingKeys.ProductRenamed);

    public Task EnqueueDeletedAsync(int id, string name, CancellationToken ct)
        => Add(new ProductDeletedEvent(id, name) { CorrelationId = CorrelationContext.Current },
            RoutingKeys.ProductDeleted);

    private Task Add<T>(T @event, string routingKey) where T : IntegrationEvent
    {
        db.OutboxMessages.Add(new OutboxMessage
        {
            EventId = @event.EventId,
            EventType = typeof(T).Name,
            Exchange = Exchanges.Catalog,
            RoutingKey = routingKey,
            Payload = JsonSerializer.Serialize(@event, JsonDefaults.Options),   // той самий JSON, що й у прямій публікації
            CorrelationId = @event.CorrelationId,
            TraceParent = Activity.Current?.Id,                                 // W3C traceparent поточного запиту
            SchemaVersion = @event.SchemaVersion,
            OccurredAt = @event.OccurredAt
        });
        return Task.CompletedTask;
    }
}