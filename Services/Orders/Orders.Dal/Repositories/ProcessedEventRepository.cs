using Dapper;
using Microsoft.Data.SqlClient;
using Orders.Dal.Abstractions;

namespace Orders.Dal.Repositories;

public sealed class ProcessedEventRepository : RepositoryBase, IProcessedEventRepository
{
    public ProcessedEventRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction) { }

    /// <summary>
    /// INSERT EventId. Порушення PRIMARY KEY (2627 / 2601) означає, що подію вже оброблено.
    /// </summary>
    public async Task<bool> TryAddAsync(Guid eventId, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);

        try
        {
            await Connection.ExecuteAsync(new CommandDefinition(
                "INSERT dbo.ProcessedEvents (EventId) VALUES (@EventId)",
                new { EventId = eventId }, Transaction, cancellationToken: ct));
            return true;
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            return false;
        }
    }
}