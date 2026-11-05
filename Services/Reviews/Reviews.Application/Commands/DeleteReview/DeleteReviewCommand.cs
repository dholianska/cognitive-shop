using MediatR;
using MongoDB.Bson;
using Reviews.Application.Common.Interfaces;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;

namespace Reviews.Application.Commands.DeleteReview;

public record DeleteReviewCommand(string Id) : ICommand;

public class DeleteReviewCommandHandler(IReviewRepository reviews) : IRequestHandler<DeleteReviewCommand>
{
    public async Task Handle(DeleteReviewCommand cmd, CancellationToken ct)
    {
        if (!await reviews.DeleteAsync(ObjectId.Parse(cmd.Id), ct))
            throw new NotFoundException($"Review {cmd.Id} not found");
    }
}