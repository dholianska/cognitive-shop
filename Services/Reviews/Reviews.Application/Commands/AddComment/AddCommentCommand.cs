using MediatR;
using MongoDB.Bson;
using Reviews.Application.Common.Interfaces;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Commands.AddComment;

public record AddCommentCommand(string ReviewId, string AuthorId, string AuthorName, string Text) : ICommand;

public class AddCommentCommandHandler(IReviewRepository reviews) : IRequestHandler<AddCommentCommand>
{
    public async Task Handle(AddCommentCommand cmd, CancellationToken ct)
    {
        var review = await reviews.GetByIdAsync(ObjectId.Parse(cmd.ReviewId), ct)
                     ?? throw new NotFoundException($"Review {cmd.ReviewId} not found");

        review.AddComment(new Author(cmd.AuthorId, cmd.AuthorName), cmd.Text);
        await reviews.UpdateAsync(review, ct);   // перевірка версії, як і в інших командах запису
    }
}