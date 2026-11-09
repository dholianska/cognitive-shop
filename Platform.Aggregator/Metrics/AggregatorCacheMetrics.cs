using System.Diagnostics.Metrics;

namespace Platform.Aggregator.Metrics;

public sealed class AggregatorCacheMetrics
{
    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;

    public AggregatorCacheMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Aggregator.Cache");
        _hits = meter.CreateCounter<long>("cache.hits");
        _misses = meter.CreateCounter<long>("cache.misses");
    }

    public void Hit() => _hits.Add(1, new KeyValuePair<string, object?>("cache.level", "L2"));
    public void Miss() => _misses.Add(1, new KeyValuePair<string, object?>("cache.level", "L2"));
}