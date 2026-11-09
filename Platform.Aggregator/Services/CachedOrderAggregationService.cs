using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Platform.Aggregator.Metrics;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Services;

/// <summary>Кеш Redis навколо gRPC-агрегації. Збій Redis не ламає запит.</summary>
public class CachedOrderAggregationService(
    GrpcOrderAggregationService inner,
    IDistributedCache cache,
    AggregatorCacheMetrics metrics,
    ILogger<CachedOrderAggregationService> logger)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    public async Task<FullOrderDto?> GetFullOrderAsync(long orderId, bool useCache, CancellationToken ct)
    {
        var key = $"aggregator:order:{orderId}:full";

        // кеш перевіряється ДО gRPC-викликів
        if (useCache)
        {
            try
            {
                var bytes = await cache.GetAsync(key, ct);
                if (bytes is not null)
                {
                    metrics.Hit();
                    logger.LogInformation("Cache hit {Key}, {Size} bytes", key, bytes.Length);
                    return JsonSerializer.Deserialize<FullOrderDto>(bytes);
                }

                metrics.Miss();
                logger.LogInformation("Cache miss {Key}", key);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Redis unavailable, cache lookup skipped for {Key}", key);
            }
        }

        var result = await inner.GetFullOrderAsync(orderId, ct);

        // часткові відповіді не кешуємо
        if (useCache && result is not null && result.Warnings.Count == 0)
        {
            try
            {
                await cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(result),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl }, ct);
                logger.LogDebug("Cache set {Key}, ttl {Ttl} s", key, Ttl.TotalSeconds);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Redis unavailable, cache write skipped for {Key}", key);
            }
        }
        else if (result is { Warnings.Count: > 0 })
        {
            logger.LogInformation("Partial response for {Key} is not cached ({Count} warnings)", key, result.Warnings.Count);
        }

        return result;
    }
}