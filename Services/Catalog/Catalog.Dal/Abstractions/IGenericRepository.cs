namespace Catalog.Dal.Abstractions;

/// <summary>
/// Спільні операції для всіх репозиторіїв на EF Core.
/// Репозиторій лише змінює стан сутностей у контексті: у базу вони потрапляють
/// єдиним IUnitOfWork.SaveChangesAsync().
/// </summary>
public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);

    // не звертаються до БД, лише змінюють стан у контексті, тому синхронні
    void Update(T entity);
    void Delete(T entity);
}