using Dapper;
using Microsoft.Data.SqlClient;
using Orders.Dal.Abstractions;

namespace Orders.Dal.Repositories;

/// <summary>
/// Спільний CRUD для репозиторіїв на Dapper. Ім'я таблиці передає конкретний репозиторій
/// як КОНСТАНТУ з коду (не вхідні дані користувача), тому його підстановка в текст запиту
/// безпечна; усі ЗНАЧЕННЯ передаються лише параметрами.
/// </summary>
public abstract class BaseDapperRepository<T> : RepositoryBase, IGenericRepository<T> where T : class
{
    protected readonly string TableName;

    /// <summary>INSERT з OUTPUT INSERTED.Id: запит повертає Id нового рядка.</summary>
    protected abstract string InsertSql { get; }

    protected BaseDapperRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction, string tableName)
        : base(connection, currentTransaction)
    {
        TableName = tableName;
    }

    public virtual async Task<T?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        var sql = $"SELECT * FROM {TableName} WHERE Id = @Id AND IsDeleted = 0";
        return await Connection.QuerySingleOrDefaultAsync<T>(
            new CommandDefinition(sql, new { Id = id }, Transaction, cancellationToken: ct));
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        var sql = $"SELECT * FROM {TableName} WHERE IsDeleted = 0 ORDER BY Id";
        var rows = await Connection.QueryAsync<T>(
            new CommandDefinition(sql, transaction: Transaction, cancellationToken: ct));
        return rows.AsList();
    }

    public virtual async Task<long> AddAsync(T entity, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        // Dapper бере значення для параметрів (@Name, @Price...) з властивостей entity
        return await Connection.ExecuteScalarAsync<long>(
            new CommandDefinition(InsertSql, entity, Transaction, cancellationToken: ct));
    }

    /// <summary>М'яке видалення. Повертає false, якщо активного запису з таким Id немає.</summary>
    public virtual async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        var sql = $"UPDATE {TableName} SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME() " +
                  "WHERE Id = @Id AND IsDeleted = 0";
        var rows = await Connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, Transaction, cancellationToken: ct));
        return rows > 0;
    }
}