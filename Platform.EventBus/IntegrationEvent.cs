namespace Platform.EventBus;

/// <summary>
/// Базова подія. Незмінна (record + init): EventId потрібен для ідемпотентності,
/// CorrelationId для наскрізного трасування (Лаб. 05).
/// </summary>
public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string? CorrelationId { get; init; }

    /// <summary>Версія схеми: нові поля додаємо, наявні не перейменовуємо і не видаляємо.</summary>
    public int SchemaVersion { get; init; } = 1;
}