namespace Catalog.Bll.Caching;

/// <summary>Політика життя запису: L1 (пам'ять) завжди коротша за L2 (Redis).</summary>
public sealed record CachePolicy(TimeSpan? L1Sliding, TimeSpan L1Absolute, TimeSpan L2Absolute)
{
    /// <summary>Картка товару: гаряче читання, змінюється відносно часто.</summary>
    public static readonly CachePolicy ProductCard =
        new(L1Sliding: TimeSpan.FromSeconds(30), L1Absolute: TimeSpan.FromMinutes(1), L2Absolute: TimeSpan.FromMinutes(10));

    /// <summary>Довідник категорій: майже не змінюється, тому лише absolute.</summary>
    public static readonly CachePolicy Categories =
        new(L1Sliding: null, L1Absolute: TimeSpan.FromMinutes(5), L2Absolute: TimeSpan.FromHours(1));
}