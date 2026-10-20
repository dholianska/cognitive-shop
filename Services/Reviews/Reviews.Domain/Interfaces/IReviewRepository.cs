using MongoDB.Bson;
using Reviews.Domain.Entities;

namespace Reviews.Domain.Interfaces;

/// <summary>Контракт доступу до відгуків. Реалізація (MongoDB) у Reviews.Infrastructure.</summary>
public interface IReviewRepository
{
    Task<Review?> GetByIdAsync(ObjectId id, CancellationToken ct = default);

    /// <summary>Чи залишав цей автор відгук на цей товар (правило "один відгук від автора на товар").</summary>
    Task<bool> ExistsAsync(long productId, string authorId, CancellationToken ct = default);

    Task AddAsync(Review review, CancellationToken ct = default);

    /// <summary>
    /// Оновлення з оптимістичною конкурентністю: застосовується лише до документа з очікуваною версією,
    /// інакше кидає ConflictException.
    /// </summary>
    Task UpdateAsync(Review review, CancellationToken ct = default);

    /// <summary>Видаляє відгук. Повертає false, якщо такого немає.</summary>
    Task<bool> DeleteAsync(ObjectId id, CancellationToken ct = default);
}