namespace Platform.EventBus;

/// <summary>Готове до публікації повідомлення (наприклад, із таблиці outbox): тіло вже серіалізоване.</summary>
public sealed record OutboundMessage(
    Guid EventId,
    string EventType,
    string Exchange,
    string RoutingKey,
    byte[] Body,
    DateTime OccurredAt,
    int SchemaVersion,
    string? CorrelationId,
    string? TraceParent);