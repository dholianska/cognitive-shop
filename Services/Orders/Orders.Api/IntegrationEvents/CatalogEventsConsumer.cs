using Platform.EventBus;
using RabbitMQ.Client;

namespace Orders.Api.IntegrationEvents;

/// <summary>Споживає події Каталогу з черги orders.catalog-sync й оновлює локальну копію Products.</summary>
public sealed class CatalogEventsConsumer(
    IConnection connection, IServiceScopeFactory scopes, ILogger<CatalogEventsConsumer> logger)
    : RabbitMqConsumer(connection, EventTopology.OrdersCatalogSync, logger)
{
    protected override async Task HandleAsync(string eventType, ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        // окремий scope на повідомлення: свій UnitOfWork (з'єднання і транзакція) на кожну подію
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;

        switch (eventType)
        {
            case nameof(ProductCreatedEvent):
                await services.GetRequiredService<ProductCreatedHandler>()
                    .HandleAsync(Deserialize<ProductCreatedEvent>(body), ct);
                break;

            case nameof(ProductPriceChangedEvent):
                await services.GetRequiredService<ProductPriceChangedHandler>()
                    .HandleAsync(Deserialize<ProductPriceChangedEvent>(body), ct);
                break;

            case nameof(ProductRenamedEvent):
                await services.GetRequiredService<ProductRenamedHandler>()
                    .HandleAsync(Deserialize<ProductRenamedEvent>(body), ct);
                break;

            case nameof(ProductDeletedEvent):
                await services.GetRequiredService<ProductDeletedHandler>()
                    .HandleAsync(Deserialize<ProductDeletedEvent>(body), ct);
                break;

            default:
                throw new PermanentFailureException($"Unknown event type '{eventType}'");
        }
    }
}