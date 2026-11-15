using System.Diagnostics.Metrics;

namespace Platform.EventBus;

/// <summary>Метрики обміну повідомленнями. Теги мають низьку кардинальність: result і queue.</summary>
public static class MessagingMetrics
{
    public const string MeterName = "Platform.Messaging";

    public static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Messages = Meter.CreateCounter<long>(
        "messaging.consumer.messages", unit: "{message}",
        description: "Messages processed by consumers by result (handled, retried, dead_lettered)");

    public static void Record(string result, string queue)
        => Messages.Add(1,
            new KeyValuePair<string, object?>("result", result),
            new KeyValuePair<string, object?>("queue", queue));
}