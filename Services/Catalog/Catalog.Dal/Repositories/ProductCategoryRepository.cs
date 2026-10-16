using Catalog.Dal.Abstractions;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

public class ProductCategoryRepository(AppDbContext context)
    : GenericRepository<ProductCategory>(context), IProductCategoryRepository
{
    // у ProductCategory складений ключ (ProductId, CategoryId): пошук за одним int неможливий
    public override Task<ProductCategory?> GetByIdAsync(int id, CancellationToken ct = default)
        => throw new NotSupportedException(
            "ProductCategory has a composite key (ProductId, CategoryId); a single Id lookup is not supported");

    // M:N через проміжну сутність і її власне поле AddedAt. Усе (фільтр, сортування, вибір товарів)
    // виконується в ОДНОМУ SQL-запиті на боці бази.
    public async Task<IReadOnlyList<Product>> GetRecentlyAddedAsync(
        string categoryName, DateTime since, CancellationToken ct = default)
        => await Set
            .Where(pc => pc.Category.Name == categoryName && pc.AddedAt >= since)
            .OrderByDescending(pc => pc.AddedAt)
            .Select(pc => pc.Product)
            .AsNoTracking()
            .ToListAsync(ct);
}