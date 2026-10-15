using Catalog.Dal.Abstractions;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

public class CategoryRepository(AppDbContext context)
    : GenericRepository<Category>(context), ICategoryRepository
{
    // Explicit Loading: категорія береться окремо, а її товари довантажуються лише тоді, коли потрібні
    public async Task<Category?> GetWithProductsAsync(int id, CancellationToken ct = default)
    {
        var category = await Set.FirstOrDefaultAsync(c => c.Id == id, ct);   // 1-й SQL-запит
        if (category is null) return null;

        await Context.Entry(category)
            .Collection(c => c.ProductCategories)
            .Query().Include(pc => pc.Product)
            .LoadAsync(ct);                                                  // 2-й SQL-запит

        return category;
    }
}