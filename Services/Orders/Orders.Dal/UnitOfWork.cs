using System.Data;
using Microsoft.Data.SqlClient;
using Orders.Dal.Abstractions;
using Orders.Dal.Repositories;

namespace Orders.Dal;

/// <summary>
/// Unit of Work: одне з'єднання і одна транзакція на всі репозиторії бізнес-операції.
///
/// Рівень ізоляції за замовчуванням - ReadCommitted: читається лише зафіксоване, "брудне"
/// читання неможливе, а блокувань менше, ніж у Serializable. Вищий рівень дає менше аномалій
/// читання, але більше блокувань і ризик взаємних блокувань (deadlock). Для наших операцій
/// цього достатньо: від втрати оновлень залишок захищає атомарний UPDATE ... WHERE Stock >= @Quantity
/// і UPDLOCK у процедурах. Таймаут команди - стандартні 30 с SqlClient, таймаут підключення - 15 с.
///
/// Реєструється як Scoped: один екземпляр (і одне з'єднання) на HTTP-запит.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly SqlConnection _connection;
    private SqlTransaction? _transaction;

    public UnitOfWork(string connectionString)
    {
        _connection = new SqlConnection(connectionString);

        // усі репозиторії отримують ОДНЕ з'єднання і доступ до ПОТОЧНОЇ транзакції
        Customers = new CustomerRepository(_connection, () => _transaction);
        Orders = new OrderRepository(_connection, () => _transaction);
        Products = new ProductRepository(_connection, () => _transaction);
    }

    public ICustomerRepository Customers { get; }
    public IOrderRepository Orders { get; }
    public IProductRepository Products { get; }

    public async Task BeginTransactionAsync(
        IsolationLevel level = IsolationLevel.ReadCommitted, CancellationToken ct = default)
    {
        if (_transaction is not null)
            throw new InvalidOperationException("Transaction is already started");

        if (_connection.State != ConnectionState.Open)
            await _connection.OpenAsync(ct);

        _transaction = (SqlTransaction)await _connection.BeginTransactionAsync(level, ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("No active transaction to commit");

        var transaction = _transaction;
        _transaction = null;
        try
        {
            await transaction.CommitAsync(ct);
        }
        finally
        {
            await transaction.DisposeAsync();
        }
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is null) return;

        var transaction = _transaction;
        _transaction = null;
        try
        {
            await transaction.RollbackAsync(ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or SqlException)
        {
            // Процедури з Лаб. 01 працюють із XACT_ABORT ON: після THROW сервер сам скасовує
            // всю транзакцію. Тоді явний Rollback з боку C# не потрібен і може кинути помилку,
            // бо транзакції вже немає. Результат той самий (зміни скасовано), тому це ігноруємо.
        }
        finally
        {
            await transaction.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
            await _transaction.DisposeAsync();   // нескасована транзакція відкочується
        await _connection.DisposeAsync();
    }
}