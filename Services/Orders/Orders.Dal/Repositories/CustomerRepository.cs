using System.Data;
using Microsoft.Data.SqlClient;
using Orders.Dal.Abstractions;
using Orders.Domain.Models;

namespace Orders.Dal.Repositories;

/// <summary>Репозиторій на чистому ADO.NET (без Dapper і без Generic Repository).</summary>
public sealed class CustomerRepository : RepositoryBase, ICustomerRepository
{
    private const string SelectCustomer = "SELECT Id, Email, FullName FROM dbo.Customers ";

    public CustomerRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction) { }

    public async Task<Customer?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        const string sql = SelectCustomer + "WHERE Id = @Id AND IsDeleted = 0";

        await EnsureOpenAsync(ct);
        await using var command = new SqlCommand(sql, Connection, Transaction);
        command.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;   // лише параметр

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    public async Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        const string sql = SelectCustomer + "WHERE Email = @Email AND IsDeleted = 0";

        await EnsureOpenAsync(ct);
        await using var command = new SqlCommand(sql, Connection, Transaction);
        command.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = email;

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = SelectCustomer + "WHERE IsDeleted = 0 ORDER BY Id";

        await EnsureOpenAsync(ct);
        await using var command = new SqlCommand(sql, Connection, Transaction);

        var result = new List<Customer>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(Map(reader));
        return result;
    }

    /// <summary>Викликає процедуру usp_CreateCustomer, яка повертає Id нового клієнта.</summary>
    public async Task<long> AddAsync(Customer customer, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await using var command = new SqlCommand("dbo.usp_CreateCustomer", Connection, Transaction)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = customer.Email;
        command.Parameters.Add("@FullName", SqlDbType.NVarChar, 200).Value = customer.FullName;

        var id = await command.ExecuteScalarAsync(ct);
        return Convert.ToInt64(id);
    }

    /// <summary>Викликає процедуру usp_SoftDeleteCustomer (м'яке видалення).</summary>
    public async Task SoftDeleteAsync(long id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await using var command = new SqlCommand("dbo.usp_SoftDeleteCustomer", Connection, Transaction)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@CustomerId", SqlDbType.BigInt).Value = id;
        await command.ExecuteNonQueryAsync(ct);
    }

    // ручне мапінг рядка reader -> модель
    private static Customer Map(SqlDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        Email = reader.GetString(1),
        FullName = reader.GetString(2)
    };
}