using Catalog.Dal.Abstractions;
using Catalog.Dal.Repositories;

namespace Catalog.Dal;

/// <summary>
/// Усі репозиторії працюють з одним DbContext. SaveChangesAsync() викликається один раз
/// наприкінці бізнес-операції, і всі зміни потрапляють у базу в одній транзакції.
/// Реєструється як Scoped: один екземпляр (і один DbContext) на HTTP-запит.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
        Products = new ProductRepository(context);
        Categories = new CategoryRepository(context);
        ProductCategories = new ProductCategoryRepository(context);
    }

    public IProductRepository Products { get; }
    public ICategoryRepository Categories { get; }
    public IProductCategoryRepository ProductCategories { get; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}