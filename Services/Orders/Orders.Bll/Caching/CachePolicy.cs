namespace Orders.Bll.Caching;

/// <summary>Політика життя запису: L1 (пам'ять) завжди коротша за L2 (Redis).</summary>
public sealed record CachePolicy(TimeSpan? L1Sliding, TimeSpan L1Absolute, TimeSpan L2Absolute)
{
    /// <summary>Замовлення містить e-mail доставки (персональні дані), тому TTL короткі.</summary>
    public static readonly CachePolicy Order =
        new(L1Sliding: null, L1Absolute: TimeSpan.FromSeconds(15), L2Absolute: TimeSpan.FromSeconds(60));
}