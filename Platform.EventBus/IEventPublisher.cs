namespace Platform.EventBus;

public interface IEventPublisher
{
    /// <summary>Серіалізує подію в JSON і публікує її.</summary>
    Task PublishAsync<T>(T @event, string exchange, string routingKey, CancellationToken ct = default)
        where T : IntegrationEvent;

    /// <summary>Публікує готове повідомлення (outbox): без повторної серіалізації, з відновленням traceparent.</summary>
    Task PublishRawAsync(OutboundMessage message, CancellationToken ct = default);
}