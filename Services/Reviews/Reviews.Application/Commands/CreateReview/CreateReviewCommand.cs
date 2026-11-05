using MediatR;
using Reviews.Application.Common.Interfaces;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Commands.CreateReview;

/// <summary>Створення відгуку. Повертає рядковий ObjectId нового документа.</summary>
public record CreateReviewCommand(
    long ProductId, string ProductName,
    string AuthorId, string AuthorName,
    int Rating, string Text) : ICommand<string>;

public class CreateReviewCommandHandler(IReviewRepository reviews)
    : IRequestHandler<CreateReviewCommand, string>
{
    public async Task<string> Handle(CreateReviewCommand cmd, CancellationToken ct)
    {
        // правила моделі діють і тут: Value Objects та сутність перевіряють дані в конструкторах
        var review = new Review(
            cmd.ProductId, cmd.ProductName,
            new Author(cmd.AuthorId, cmd.AuthorName),
            new Rating(cmd.Rating),
            cmd.Text);

        await reviews.AddAsync(review, ct);
        return review.Id.ToString();
    }
}