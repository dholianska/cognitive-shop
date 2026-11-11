using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Platform.Aggregator.Metrics;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Services;

/// <summary>Кеш Redis навколо gRPC-агрегації. Збій Redis не ламає запит і не гальмує його.</summary>
public class CachedOrderAggregationService(
    GrpcOrderAggregationService inner,
    IDistributedCache cache,
    AggregatorCacheMetrics metrics,
    ILogger<CachedOrderAggregationService> logger)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    // Graceful degradation: короткий таймаут і пауза після збою Redis (стан спільний для всіх запитів)
    private static readonly TimeSpan CacheTimeout = TimeSpan.FromSeconds(1);
    private const long PauseMs = 15_000;
    private static long _pausedUntil;   // Environment.TickCount64

    private static bool Paused => Environment.TickCount64 < Interlocked.Read(ref _pausedUntil);
    private static void Pause() => Interlocked.Exchange(ref _pausedUntil, Environment.TickCount64 + PauseMs);

    public async Task<FullOrderDto?> GetFullOrderAsync(long orderId, bool useCache, CancellationToken ct)
    {
        var key = $"aggregator:order:{orderId}:full";
        var cacheEnabled = useCache && !Paused;

        // кеш перевіряється ДО gRPC-викликів
        if (cacheEnabled)
        {
            try
            {
                var bytes = await cache.GetAsync(key, ct).WaitAsync(CacheTimeout, ct);
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
                Pause();
                logger.LogWarning(ex, "Redis unavailable, cache paused for {Pause} s, lookup skipped for {Key}",
                    PauseMs / 1000, key);
                cacheEnabled = false;
            }
        }

        var result = await inner.GetFullOrderAsync(orderId, ct);

        // часткові відповіді не кешуємо
        if (cacheEnabled && !Paused && result is not null && result.Warnings.Count == 0)
        {
            try
            {
                await cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(result),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl }, ct)
                    .WaitAsync(CacheTimeout, ct);
                logger.LogDebug("Cache set {Key}, ttl {Ttl} s", key, Ttl.TotalSeconds);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Pause();
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