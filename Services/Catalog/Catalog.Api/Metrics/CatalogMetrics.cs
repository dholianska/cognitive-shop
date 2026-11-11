using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Catalog.Api.Metrics;

/// <summary>Бізнес-метрики каталогу. Singleton: живе протягом усього часу роботи застосунку.</summary>
public sealed class CatalogMetrics
{
    private readonly Counter<long> _created;
    private readonly Counter<long> _updated;
    private readonly Counter<long> _deleted;
    private readonly Histogram<double> _duration;
    private int _inFlight;

    public CatalogMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Catalog.Api");

        _created = meter.CreateCounter<long>(
            "products.created", unit: "{product}", description: "Number of created products");
        _updated = meter.CreateCounter<long>(
            "products.updated", unit: "{product}", description: "Number of product updates by type");
        _deleted = meter.CreateCounter<long>(
            "products.deleted", unit: "{product}", description: "Number of deleted products");
        _duration = meter.CreateHistogram<double>(
            "catalog.operation.duration", unit: "ms", description: "Duration of catalog operations");

        meter.CreateObservableGauge(
            "catalog.operations.in_flight", () => Volatile.Read(ref _inFlight),
            unit: "{operation}", description: "Catalog operations being processed right now");
    }

    public void ProductCreated() => _created.Add(1);
    public void ProductDeleted() => _deleted.Add(1);

    /// <param name="type">price | stock_added | stock_removed | category_assigned</param>
    public void ProductUpdated(string type) => _updated.Add(1, Tag("update.type", type));

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