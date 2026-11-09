namespace Orders.Bll.Caching;

public static class CacheKeys
{
    public static string Order(long id) => $"orders:order:{id}";
}