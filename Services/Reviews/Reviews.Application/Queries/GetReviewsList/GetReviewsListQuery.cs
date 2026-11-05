using MediatR;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;

namespace Reviews.Application.Queries.GetReviewsList;

public record GetReviewsListQuery(long? ProductId, int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<ReviewDto>>;

public class GetReviewsListQueryHandler(IReviewQueries queries)
    : IRequestHandler<GetReviewsListQuery, PagedResult<ReviewDto>>
{
    private const int MaxPageSize = 100;

    // нормалізація: сторінка від 1 (верхня межа захищає від переповнення в skip), розмір 1..100;
    // сама пагінація і фільтр виконуються в MongoDB, а не в пам'яті
    public Task<PagedResult<ReviewDto>> Handle(GetReviewsListQuery q, CancellationToken ct)
        => queries.GetListAsync(
            q.ProductId,
            Math.Clamp(q.Page, 1, 1_000_000),
            Math.Clamp(q.PageSize, 1, MaxPageSize),
            ct);
}