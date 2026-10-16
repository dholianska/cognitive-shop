using Catalog.Dal.Abstractions;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

public class ProductRepository(AppDbContext context)
    : GenericRepository<Product>(context), IProductRepository
{
    // Eager Loading: Include/ThenInclude додають JOIN, тому все приходить ОДНИМ запитом
    public Task<Product?> GetWithDetailsAsync(int id, CancellationToken ct = default)
        => Set.Include(p => p.Detail)
              .Include(p => p.Images)
              .Include(p => p.ProductCategories).ThenInclude(pc => pc.Category)
              .FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> ExistsBySkuAsync(string sku, CancellationToken ct = default)
        => Set.AnyAsync(p => p.Sku == sku, ct);
}