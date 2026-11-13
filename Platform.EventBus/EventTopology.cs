using RabbitMQ.Client;

namespace Platform.EventBus;

public static class Exchanges
{
    /// <summary>Події Каталогу (topic exchange).</summary>
    public const string Catalog = "catalog-events";
}

public static class RoutingKeys
{
    public const string ProductCreated = "product.created";
    public const string ProductPriceChanged = "product.price_changed";
    public const string ProductRenamed = "product.renamed";
    public const string ProductDeleted = "product.deleted";
}

/// <summary>Черга сервісу-споживача з прив'язками до exchange та своєю DLQ.</summary>
public sealed record ConsumerQueue(string Exchange, string Queue, IReadOnlyList<string> BindingKeys)
{
    public string DeadLetterExchange => $"{Exchange}.dlx";
    public string DeadLetterQueue => $"{Queue}.dlq";
}

public static class EventTopology
{
    /// <summary>Замовлення оновлюють локальну копію Products: слухають усі події товару.</summary>
    public static readonly ConsumerQueue OrdersCatalogSync =
        new(Exchanges.Catalog, "orders.catalog-sync", ["product.*"]);

    /// <summary>Відгуки оновлюють лише productName: слухають тільки перейменування.</summary>
    public static readonly ConsumerQueue ReviewsCatalogSync =
        new(Exchanges.Catalog, "reviews.catalog-sync", [RoutingKeys.ProductRenamed]);

    public static IReadOnlyList<ConsumerQueue> All { get; } = [OrdersCatalogSync, ReviewsCatalogSync];

    public static Task DeclareExchangeAsync(IChannel channel, string exchange, CancellationToken ct)
        => channel.ExchangeDeclareAsync(exchange, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: ct);

    /// <summary>Оголошує exchange, DLX, DLQ, основну чергу з x-dead-letter-exchange і bindings.</summary>
    public static async Task DeclareConsumerQueueAsync(IChannel channel, ConsumerQueue q, CancellationToken ct)
    {
        await DeclareExchangeAsync(channel, q.Exchange, ct);

        // DLX типу direct: routing key = назва основної черги, тож кожна DLQ отримує лише свої повідомлення
        await channel.ExchangeDeclareAsync(q.DeadLetterExchange, ExchangeType.Direct, durable: true,
            autoDelete: false, cancellationToken: ct);
        await channel.QueueDeclareAsync(q.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: null, cancellationToken: ct);
        await channel.QueueBindAsync(q.DeadLetterQueue, q.DeadLetterExchange, routingKey: q.Queue,
            cancellationToken: ct);

        // основна черга: відхилені повідомлення (Nack без requeue) автоматично йдуть у DLX
        await channel.QueueDeclareAsync(q.Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = q.DeadLetterExchange,
                ["x-dead-letter-routing-key"] = q.Queue
            },
            cancellationToken: ct);

        foreach (var key in q.BindingKeys)
            await channel.QueueBindAsync(q.Queue, q.Exchange, routingKey: key, cancellationToken: ct);
    }

    /// <summary>Уся топологія платформи (її оголошує publisher, щоб черги існували до першої події).</summary>
    public static async Task DeclareAllAsync(IChannel channel, CancellationToken ct)
    {
        foreach (var queue in All)
            await DeclareConsumerQueueAsync(channel, queue, ct);
    }
}