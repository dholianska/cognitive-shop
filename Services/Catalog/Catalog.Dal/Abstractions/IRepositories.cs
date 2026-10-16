using Catalog.Domain.Entities;

namespace Catalog.Dal.Abstractions;

public interface IProductRepository : IGenericRepository<Product>
{
    /// <summary>Eager Loading: картка товару з деталями, зображеннями й категоріями одним запитом.</summary>
    Task<Product?> GetWithDetailsAsync(int id, CancellationToken ct = default);

    Task<bool> ExistsBySkuAsync(string sku, CancellationToken ct = default);
}

public interface ICategoryRepository : IGenericRepository<Category>
{
    /// <summary>Explicit Loading: категорія окремим запитом, а її товари довантажуються другим.</summary>
    Task<Category?> GetWithProductsAsync(int id, CancellationToken ct = default);
}

public interface IProductCategoryRepository : IGenericRepository<ProductCategory>
{
    /// <summary>LINQ to Entities: товари категорії, додані після вказаної дати (M:N + поле AddedAt).</summary>
    Task<IReadOnlyList<Product>> GetRecentlyAddedAsync(
        string categoryName, DateTime since, CancellationToken ct = default);
}