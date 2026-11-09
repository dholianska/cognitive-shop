namespace Reviews.Application.Caching;

public static class ReviewCacheKeys
{
    public static string Review(string id) => $"reviews:review:{id}";

    /// <summary>Ключ сторінки списку; версію додає CachingBehavior.</summary>
    public static string List(long? productId, int page, int pageSize)
        => $"reviews:list:{(productId?.ToString() ?? "all")}:p{page}:s{pageSize}";

    /// <summary>Спільна версія всіх списків: зміна версії інвалідує всі списки одразу.</summary>
    public const string ListVersion = "reviews:list-version";
}