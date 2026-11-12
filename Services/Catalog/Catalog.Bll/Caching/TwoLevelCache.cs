using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Catalog.Bll.Caching;

public sealed class TwoLevelCache(
    IMemoryCache memory,
    IDistributedCache distributed,
    ICacheMetrics metrics,
    ILogger<TwoLevelCache> logger) : ITwoLevelCache
{
    // Graceful degradation: короткий таймаут операцій L2 і пауза L2 після збою,
    // щоб зупинений Redis не змушував кожен запит чекати таймаут
    private static readonly TimeSpan L2Timeout = TimeSpan.FromSeconds(1);
    private const long L2PauseMs = 15_000;
    private long _l2PausedUntil;   // Environment.TickCount64

    private bool L2Paused => Environment.TickCount64 < Interlocked.Read(ref _l2PausedUntil);
    private void PauseL2() => Interlocked.Exchange(ref _l2PausedUntil, Environment.TickCount64 + L2PauseMs);

    public async Task<T?> GetOrCreateAsync<T>(string key, CachePolicy policy,
        Func<CancellationToken, Task<T?>> factory, CancellationToken ct = default) where T : class
    {
        // ---- L1: пам'ять ----
        if (memory.TryGetValue(key, out T? local) && local is not null)
        {
            metrics.Hit("L1");
            logger.LogDebug("Cache hit {Key}, level {Level}", key, "L1");
            return local;
        }
        metrics.Miss("L1");

        // ---- L2: Redis (пропускаємо, поки він «лежить») ----
        if (!L2Paused)
        {
            try
            {
                var bytes = await distributed.GetAsync(key, ct).WaitAsync(L2Timeout, ct);
                if (bytes is not null)
                {
                    var shared = JsonSerializer.Deserialize<T>(bytes);
                    if (shared is not null)
                    {
                        metrics.Hit("L2");
                        logger.LogDebug("Cache hit {Key}, level {Level}, {Size} bytes", key, "L2", bytes.Length);
                        SetLocal(key, shared, policy);          // L1 заповнюється на зворотному шляху
                        return shared;
                    }
                }
                metrics.Miss("L2");
                logger.LogDebug("Cache miss {Key}, level {Level}", key, "L2");
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                PauseL2();
                logger.LogWarning(ex,
                    "Redis (L2) unavailable for {Key}, L2 paused for {Pause} s, falling back to database",
                    key, L2PauseMs / 1000);
            }
        }

        // ---- джерело: база даних ----
        var created = await factory(ct);
        if (created is null) return null;

        await SetSharedAsync(key, created, policy, ct);     // L2
        SetLocal(key, created, policy);                     // L1
        return created;
    }

    public async Task RemoveAsync(string key, string reason, CancellationToken ct = default)
    {
        memory.Remove(key);

        // L2 чистимо завжди (щоб не лишити застарілий запис), але з коротким таймаутом
        try
        {
            await distributed.RemoveAsync(key, ct).WaitAsync(L2Timeout, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            PauseL2();
            logger.LogWarning(ex, "Redis (L2) invalidation failed for {Key}", key);
        }

        metrics.Invalidated();
        logger.LogInformation("Cache invalidated {Key}, reason {Reason}", key, reason);
    }

    private void SetLocal<T>(string key, T value, CachePolicy policy) where T : class
        => memory.Set(key, value, new MemoryCacheEntryOptions
        {
            Size = 1,
            SlidingExpiration = policy.L1Sliding,
            AbsoluteExpirationRelativeToNow = policy.L1Absolute
        });

    private async Task SetSharedAsync<T>(string key, T value, CachePolicy policy, CancellationToken ct)
    {
        if (L2Paused) return;

        try
        {
            await distributed.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(value),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = policy.L2Absolute }, ct)
                .WaitAsync(L2Timeout, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            PauseL2();
            logger.LogWarning(ex, "Redis (L2) write failed for {Key}", key);
        }
    }
}