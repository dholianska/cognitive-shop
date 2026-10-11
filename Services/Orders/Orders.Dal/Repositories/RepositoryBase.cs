using System.Data;
using Microsoft.Data.SqlClient;

namespace Orders.Dal.Repositories;

/// <summary>
/// Спільна основа всіх репозиторіїв: одне з'єднання і доступ до ПОТОЧНОЇ транзакції
/// Unit of Work (або null, якщо транзакцію не розпочато).
/// </summary>
public abstract class RepositoryBase
{
    protected readonly SqlConnection Connection;
    private readonly Func<SqlTransaction?> _currentTransaction;

    protected RepositoryBase(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
    {
        Connection = connection;
        _currentTransaction = currentTransaction;
    }

    protected SqlTransaction? Transaction => _currentTransaction();

    protected async Task EnsureOpenAsync(CancellationToken ct)
    {
        if (Connection.State != ConnectionState.Open)
            await Connection.OpenAsync(ct);
    }
}