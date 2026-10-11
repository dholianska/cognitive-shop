namespace Orders.Dal.Abstractions;

/// <summary>Спільні CRUD-операції для всіх репозиторіїв, побудованих на Dapper.</summary>
public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<long> AddAsync(T entity, CancellationToken ct = default);

    /// <summary>М'яке видалення (IsDeleted = 1). Повертає false, якщо запису немає.</summary>
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}