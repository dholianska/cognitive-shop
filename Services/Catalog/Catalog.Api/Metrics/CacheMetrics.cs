using System.Diagnostics.Metrics;
using Catalog.Bll.Caching;

namespace Catalog.Api.Metrics;

public sealed class CacheMetrics : ICacheMetrics
{
    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;
    private readonly Counter<long> _invalidations;

    public CacheMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Catalog.Cache");
        _hits = meter.CreateCounter<long>("cache.hits");
        _misses = meter.CreateCounter<long>("cache.misses");
        _invalidations = meter.CreateCounter<long>("cache.invalidations");
    }

    public void Hit(string level) => _hits.Add(1, new KeyValuePair<string, object?>("cache.level", level));
    public void Miss(string level) => _misses.Add(1, new KeyValuePair<string, object?>("cache.level", level));
    public void Invalidated() => _invalidations.Add(1);
}