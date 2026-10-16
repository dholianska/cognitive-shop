using Ardalis.Specification;

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

    /// <summary>Вибірка за специфікацією (лише читання). Критерії описує сама специфікація.</summary>
    Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec, CancellationToken ct = default);

    /// <summary>Кількість записів, що відповідають специфікації (без Skip/Take).</summary>
    Task<int> CountAsync(ISpecification<T> spec, CancellationToken ct = default);
}