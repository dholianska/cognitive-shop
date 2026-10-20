using Reviews.Application.Common.Models;

namespace Reviews.Application.Common.Interfaces;

/// <summary>
/// Контракт читання (сторона Query в CQRS). Application не знає, як саме дані дістаються з бази:
/// реалізація в Infrastructure робить проєкцію одразу в DTO і пагінацію засобами MongoDB (skip/limit).
/// </summary>
public interface IReviewQueries
{
    Task<ReviewDto?> GetByIdAsync(string id, CancellationToken ct);

    Task<PagedResult<ReviewDto>> GetListAsync(long? productId, int page, int pageSize, CancellationToken ct);
}