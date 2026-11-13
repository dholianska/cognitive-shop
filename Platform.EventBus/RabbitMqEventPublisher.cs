using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Platform.EventBus;

public sealed class RabbitMqEventPublisher(IConnection connection, ILogger<RabbitMqEventPublisher> logger)
    : IEventPublisher
{
    public Task PublishAsync<T>(T @event, string exchange, string routingKey, CancellationToken ct = default)
        where T : IntegrationEvent
        => PublishRawAsync(new OutboundMessage(
            @event.EventId, typeof(T).Name, exchange, routingKey,
            JsonSerializer.SerializeToUtf8Bytes(@event, JsonDefaults.Options),
            @event.OccurredAt, @event.SchemaVersion, @event.CorrelationId, TraceParent: null), ct);

    public async Task PublishRawAsync(OutboundMessage message, CancellationToken ct = default)
    {
        // span публікації: нащадок поточного запиту або (для outbox) відновленого traceparent
        using var activity = TracePropagation.StartPublishActivity(
            message.Exchange, message.RoutingKey, message.EventId.ToString(), message.EventType, message.TraceParent);
        if (!string.IsNullOrEmpty(message.CorrelationId))
            activity?.SetTag(CorrelationContext.TagName, message.CorrelationId);

        try
        {
            // канал з підтвердженнями: BasicPublishAsync чекає, доки брокер прийме повідомлення
            await using var channel = await connection.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                ct);

            var headers = new Dictionary<string, object?> { ["x-schema-version"] = message.SchemaVersion };
            if (!string.IsNullOrEmpty(message.CorrelationId))
                headers["x-correlation-id"] = message.CorrelationId;

            var props = new BasicProperties
            {
                Persistent = true,                                   // переживає перезапуск брокера
                ContentType = "application/json",
                MessageId = message.EventId.ToString(),              // для ідемпотентності та діагностики
                Type = message.EventType,                            // тип події для маршрутизації в consumer
                Timestamp = new AmqpTimestamp(new DateTimeOffset(message.OccurredAt).ToUnixTimeSeconds()),
                Headers = headers
            };

            TracePropagation.Inject(activity, props);                // traceparent / tracestate / baggage

            await channel.BasicPublishAsync(message.Exchange, message.RoutingKey, mandatory: false,
                basicProperties: props, body: message.Body, cancellationToken: ct);

            logger.LogInformation(
                "Published {EventType} {EventId} to {Exchange}/{RoutingKey}, correlation {CorrelationId}",
                message.EventType, message.EventId, message.Exchange, message.RoutingKey, message.CorrelationId);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}