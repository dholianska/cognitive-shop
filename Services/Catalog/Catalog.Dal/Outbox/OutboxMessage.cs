namespace Catalog.Dal.Outbox;

/// <summary>
/// Подія, записана в тій самій транзакції, що й бізнес-зміна (Transactional Outbox).
/// Її публікує OutboxDispatcher після commit.
/// </summary>
public class OutboxMessage
{
    public long Id { get; set; }
    public Guid EventId { get; set; }
    public string EventType { get; set; } = "";
    public string Exchange { get; set; } = "";
    public string RoutingKey { get; set; } = "";

    /// <summary>JSON події (самодостатній, формат ідентичний прямій публікації).</summary>
    public string Payload { get; set; } = "";

    public string? CorrelationId { get; set; }

    /// <summary>W3C traceparent запиту, що створив подію: щоб trace лишався наскрізним попри затримку.</summary>
    public string? TraceParent { get; set; }

    public int SchemaVersion { get; set; } = 1;
    public DateTime OccurredAt { get; set; }

    /// <summary>null: ще не опубліковано.</summary>
    public DateTime? ProcessedAt { get; set; }

    public int Attempts { get; set; }
    public string? LastError { get; set; }
}