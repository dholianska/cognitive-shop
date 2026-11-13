using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Platform.EventBus;

public sealed class RabbitMqEventPublisher(IConnection connection, ILogger<RabbitMqEventPublisher> logger)
    : IEventPublisher
{
    public async Task PublishAsync<T>(T @event, string exchange, string routingKey, CancellationToken ct = default)
        where T : IntegrationEvent
    {
        // канал з підтвердженнями: BasicPublishAsync чекає, доки брокер прийме повідомлення
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            ct);

        var headers = new Dictionary<string, object?> { ["x-schema-version"] = @event.SchemaVersion };
        if (!string.IsNullOrEmpty(@event.CorrelationId))
            headers["x-correlation-id"] = @event.CorrelationId;

        var props = new BasicProperties
        {
            Persistent = true,                                   // переживає перезапуск брокера
            ContentType = "application/json",
            MessageId = @event.EventId.ToString(),               // для ідемпотентності та діагностики
            Type = typeof(T).Name,                               // тип події для маршрутизації в consumer
            Timestamp = new AmqpTimestamp(new DateTimeOffset(@event.OccurredAt).ToUnixTimeSeconds()),
            Headers = headers
        };

        var body = JsonSerializer.SerializeToUtf8Bytes(@event, JsonDefaults.Options);

        await channel.BasicPublishAsync(exchange, routingKey, mandatory: false, basicProperties: props,
            body: body, cancellationToken: ct);

        logger.LogInformation(
            "Published {EventType} {EventId} to {Exchange}/{RoutingKey}, correlation {CorrelationId}",
            typeof(T).Name, @event.EventId, exchange, routingKey, @event.CorrelationId);
    }
}