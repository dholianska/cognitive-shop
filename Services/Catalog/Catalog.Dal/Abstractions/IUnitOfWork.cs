namespace Catalog.Dal.Abstractions;

/// <summary>
/// Об'єднує репозиторії й має єдиний SaveChangesAsync, що зберігає всі зміни однією транзакцією.
/// </summary>
public interface IUnitOfWork
{
    IProductRepository Products { get; }
    ICategoryRepository Categories { get; }
    IProductCategoryRepository ProductCategories { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}