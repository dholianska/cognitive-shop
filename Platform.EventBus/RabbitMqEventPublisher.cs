using System.Diagnostics;
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
        // span публікації: нащадок поточного HTTP-запиту, його контекст іде в заголовки повідомлення
        using var activity = TracePropagation.StartPublishActivity(
            exchange, routingKey, @event.EventId.ToString(), typeof(T).Name);
        if (!string.IsNullOrEmpty(@event.CorrelationId))
            activity?.SetTag(CorrelationContext.TagName, @event.CorrelationId);

        try
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

            TracePropagation.Inject(activity, props);                // traceparent / tracestate / baggage

            var body = JsonSerializer.SerializeToUtf8Bytes(@event, JsonDefaults.Options);

            await channel.BasicPublishAsync(exchange, routingKey, mandatory: false, basicProperties: props,
                body: body, cancellationToken: ct);

            logger.LogInformation(
                "Published {EventType} {EventId} to {Exchange}/{RoutingKey}, correlation {CorrelationId}",
                typeof(T).Name, @event.EventId, exchange, routingKey, @event.CorrelationId);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}