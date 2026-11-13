using Catalog.Dal.Abstractions;
using Catalog.Dal.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Catalog.Dal;

/// <summary>
/// Усі репозиторії працюють з одним DbContext. SaveChangesAsync() викликається один раз
/// наприкінці бізнес-операції, і всі зміни потрапляють у базу в одній транзакції.
/// Реєструється як Scoped: один екземпляр (і один DbContext) на HTTP-запит.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;

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

    public async Task BeginTransactionAsync(CancellationToken ct = default)
        => _transaction = await _context.Database.BeginTransactionAsync(ct);

    // якщо до Commit щось упаде, транзакцію відкотить Dispose контексту наприкінці запиту
    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction is null) return;

        await _transaction.CommitAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }
}