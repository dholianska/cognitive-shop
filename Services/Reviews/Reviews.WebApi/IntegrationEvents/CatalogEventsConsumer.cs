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

        var e = Deserialize<ProductRenamedEvent>(body);

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ProductRenamedHandler>().HandleAsync(e, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // бізнес-контекст збою: ідентифікатор сутності поруч з EventId і CorrelationId зі scope споживача
            Logger.LogWarning(ex, "Handling {EventType} {EventId} failed for product {ProductId}",
                eventType, e.EventId, e.ProductId);
            throw;
        }
    }
}