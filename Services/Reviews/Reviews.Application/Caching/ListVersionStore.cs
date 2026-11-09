using Microsoft.Extensions.Caching.Distributed;

namespace Reviews.Application.Caching;

public interface IListVersionStore
{
    Task<long> GetAsync(CancellationToken ct);
    Task BumpAsync(CancellationToken ct);
}

/// <summary>Версія списків живе лише в Redis: вона має бути однаковою для всіх реплік.</summary>
public sealed class ListVersionStore(IDistributedCache distributed) : IListVersionStore
{
    private static readonly DistributedCacheEntryOptions Options =
        new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7) };

    public async Task<long> GetAsync(CancellationToken ct)
        => long.TryParse(await distributed.GetStringAsync(ReviewCacheKeys.ListVersion, ct), out var v) ? v : 0;

    public Task BumpAsync(CancellationToken ct)
        => distributed.SetStringAsync(ReviewCacheKeys.ListVersion, DateTime.UtcNow.Ticks.ToString(), Options, ct);
}