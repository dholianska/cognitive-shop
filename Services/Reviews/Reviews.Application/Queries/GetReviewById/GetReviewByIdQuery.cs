using MediatR;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;
using Reviews.Domain.Exceptions;
using Reviews.Application.Caching;

namespace Reviews.Application.Queries.GetReviewById;

public record GetReviewByIdQuery(string Id) : IQuery<ReviewDto>, ICacheableQuery
{
    string ICacheableQuery.CacheKey => ReviewCacheKeys.Review(Id);
    CachePolicy ICacheableQuery.Policy => CachePolicy.Review;
    bool ICacheableQuery.VersionedList => false;
}

public class GetReviewByIdQueryHandler(IReviewQueries queries) : IRequestHandler<GetReviewByIdQuery, ReviewDto>
{
    public async Task<ReviewDto> Handle(GetReviewByIdQuery q, CancellationToken ct)
        => await queries.GetByIdAsync(q.Id, ct)
           ?? throw new NotFoundException($"Review {q.Id} not found");
}