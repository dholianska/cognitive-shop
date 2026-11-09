namespace Reviews.Application.Caching;

/// <summary>Запит, відповідь якого можна кешувати.</summary>
public interface ICacheableQuery
{
    string CacheKey { get; }
    CachePolicy Policy { get; }

    /// <summary>true для списків: до ключа додається версія списків із Redis.</summary>
    bool VersionedList { get; }
}

/// <summary>Команда запису, після якої треба інвалідувати кеш.</summary>
public interface ICacheInvalidatingCommand
{
    /// <summary>Id відгуку, чий запис видалити; null, якщо відгуку ще немає в кеші.</summary>
    string? ReviewId { get; }

    string Reason { get; }
}