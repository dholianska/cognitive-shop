using MediatR;
using MongoDB.Bson;
using Reviews.Application.Common.Interfaces;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;
using Reviews.Application.Caching;

namespace Reviews.Application.Commands.DeleteReview;

public record DeleteReviewCommand(string Id) : ICommand, ICacheInvalidatingCommand
{
    string? ICacheInvalidatingCommand.ReviewId => Id;
    string ICacheInvalidatingCommand.Reason => "ReviewDeleted";
}

public class DeleteReviewCommandHandler(IReviewRepository reviews) : IRequestHandler<DeleteReviewCommand>
{
    public async Task Handle(DeleteReviewCommand cmd, CancellationToken ct)
    {
        if (!await reviews.DeleteAsync(ObjectId.Parse(cmd.Id), ct))
            throw new NotFoundException($"Review {cmd.Id} not found");
    }
}