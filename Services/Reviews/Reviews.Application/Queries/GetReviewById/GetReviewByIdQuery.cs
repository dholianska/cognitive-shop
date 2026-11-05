using MediatR;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;
using Reviews.Domain.Exceptions;

namespace Reviews.Application.Queries.GetReviewById;

public record GetReviewByIdQuery(string Id) : IQuery<ReviewDto>;

public class GetReviewByIdQueryHandler(IReviewQueries queries) : IRequestHandler<GetReviewByIdQuery, ReviewDto>
{
    public async Task<ReviewDto> Handle(GetReviewByIdQuery q, CancellationToken ct)
        => await queries.GetByIdAsync(q.Id, ct)
           ?? throw new NotFoundException($"Review {q.Id} not found");
}