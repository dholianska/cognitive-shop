namespace Reviews.Application.Caching;

/// <summary>Політика життя запису: L1 (пам'ять) завжди коротша за L2 (Redis).</summary>
public sealed record CachePolicy(TimeSpan? L1Sliding, TimeSpan L1Absolute, TimeSpan L2Absolute)
{
    /// <summary>Один відгук (читається за Id, змінюється рідко).</summary>
    public static readonly CachePolicy Review =
        new(L1Sliding: TimeSpan.FromSeconds(30), L1Absolute: TimeSpan.FromMinutes(1), L2Absolute: TimeSpan.FromMinutes(10));

    /// <summary>Сторінка списку відгуків товару (ключ містить версію списків).</summary>
    public static readonly CachePolicy ReviewList =
        new(L1Sliding: null, L1Absolute: TimeSpan.FromSeconds(30), L2Absolute: TimeSpan.FromMinutes(5));
}