using Platform.EventBus;
using RabbitMQ.Client;

namespace Reviews.WebApi.IntegrationEvents;

/// <summary>Споживає перейменування товарів з черги reviews.catalog-sync (binding product.renamed).</summary>
public sealed class CatalogEventsConsumer(
    IConnection connection, IServiceScopeFactory scopes, ILogger<CatalogEventsConsumer> logger)
    : RabbitMqConsumer(connection, EventTopology.ReviewsCatalogSync, logger)
{
    protected override async Task HandleAsync(string eventType, ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        if (eventType != nameof(ProductRenamedEvent))
            throw new PermanentFailureException($"Unknown event type '{eventType}'");

        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ProductRenamedHandler>()
            .HandleAsync(Deserialize<ProductRenamedEvent>(body), ct);
    }
}