using Microsoft.Data.SqlClient;
using Orders.Domain.Exceptions;

namespace Orders.Dal;

/// <summary>
/// Перетворює помилки SQL Server на доменні винятки, щоб технічні деталі БД
/// не виходили за межі DAL. Повідомлення наших процедур (THROW 5000x) безпечні для показу;
/// для решти помилок повідомлення SQL Server клієнту не передається.
/// </summary>
public static class SqlErrors
{
    public static bool TryTranslate(SqlException ex, out Exception domainException)
    {
        domainException = ex.Number switch
        {
            50001 => new NotFoundException(ex.Message),            // THROW 50001: не знайдено
            50002 => new BusinessConflictException(ex.Message),    // THROW 50002: недопустимий перехід
            50003 => new ValidationException(ex.Message),          // THROW 50003: некоректні дані
            2601 or 2627 => new BusinessConflictException("Record with the same key already exists"),
            547 => new BusinessConflictException("Operation violates a data integrity constraint"),
            _ => null!
        };
        return domainException is not null;
    }
}