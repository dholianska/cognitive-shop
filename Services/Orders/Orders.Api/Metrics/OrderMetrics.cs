using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Orders.Api.Metrics;

/// <summary>Бізнес-метрики замовлень. Singleton: живе протягом усього часу роботи застосунку.</summary>
public sealed class OrderMetrics
{
    private readonly Counter<long> _created;
    private readonly Counter<long> _confirmed;
    private readonly Counter<long> _statusChanged;
    private readonly Histogram<double> _duration;
    private int _inFlight;

    public OrderMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Orders.Api");

        _created = meter.CreateCounter<long>(
            "orders.created", unit: "{order}", description: "Number of created orders");
        _confirmed = meter.CreateCounter<long>(
            "orders.confirmed", unit: "{order}", description: "Number of confirmed orders");
        _statusChanged = meter.CreateCounter<long>(
            "orders.status_changed", unit: "{order}", description: "Number of order status changes");
        _duration = meter.CreateHistogram<double>(
            "orders.operation.duration", unit: "ms", description: "Duration of order operations");

        // gauge: значення зчитується в момент збору метрик
        meter.CreateObservableGauge(
            "orders.operations.in_flight", () => Volatile.Read(ref _inFlight),
            unit: "{operation}", description: "Order operations being processed right now");
    }

    public void OrderCreated(string status) => _created.Add(1, Tag("order.status", status));
    public void OrderConfirmed() => _confirmed.Add(1);
    public void StatusChanged(string status) => _statusChanged.Add(1, Tag("order.status", status));

    /// <summary>Вимірює тривалість операції (успішної і невдалої) та кількість операцій у роботі.</summary>
    public async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> action)
    {
        Interlocked.Increment(ref _inFlight);
        var sw = Stopwatch.StartNew();
        var success = false;
        try
        {
            var result = await action();
            success = true;
            return result;
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
            _duration.Record(sw.Elapsed.TotalMilliseconds,
                Tag("operation", operation), Tag("status", success ? "success" : "failure"));
        }
    }

    public Task MeasureAsync(string operation, Func<Task> action)
        => MeasureAsync<object?>(operation, async () => { await action(); return null; });

    private static KeyValuePair<string, object?> Tag(string key, object? value) => new(key, value);
}