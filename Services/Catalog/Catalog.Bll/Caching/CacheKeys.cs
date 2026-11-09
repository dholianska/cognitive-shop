namespace Catalog.Bll.Caching;

public static class CacheKeys
{
    public static string Product(int id) => $"catalog:product:{id}";

    public const string Categories = "catalog:categories";
}