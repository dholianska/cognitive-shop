namespace Reviews.Application.Caching;

public interface ITwoLevelCache
{
    /// <summary>L1 → L2 → factory (БД); проміжні рівні заповнюються на зворотному шляху.</summary>
    Task<T?> GetOrCreateAsync<T>(string key, CachePolicy policy,
        Func<CancellationToken, Task<T?>> factory, CancellationToken ct = default) where T : class;

    /// <summary>Видаляє запис з обох рівнів; reason потрапляє в лог.</summary>
    Task RemoveAsync(string key, string reason, CancellationToken ct = default);
}