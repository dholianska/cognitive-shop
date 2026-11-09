using MediatR;
using Reviews.Application.Common.Interfaces;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Reviews.Application.Caching;

namespace Reviews.Application.Commands.CreateReview;

/// <summary>Створення відгуку. Повертає рядковий ObjectId нового документа.</summary>
public record CreateReviewCommand(
    long ProductId, string ProductName,
    string AuthorId, string AuthorName,
    int Rating, string Text) : ICommand<string>, ICacheInvalidatingCommand
{
    string? ICacheInvalidatingCommand.ReviewId => null;   // нового відгуку в кеші ще нема
    string ICacheInvalidatingCommand.Reason => "ReviewCreated";
}

public class CreateReviewCommandHandler(IReviewRepository reviews, ILogger<CreateReviewCommandHandler> logger)
    : IRequestHandler<CreateReviewCommand, string>
{
    private static readonly ActivitySource Source = new("Reviews.WebApi");

    public async Task<string> Handle(CreateReviewCommand cmd, CancellationToken ct)
    {
        using var activity = Source.StartActivity("CreateReview");
        activity?.SetTag("product.id", cmd.ProductId);

        // правила моделі діють і тут: Value Objects та сутність перевіряють дані в конструкторах
        var review = new Review(
            cmd.ProductId, cmd.ProductName,
            new Author(cmd.AuthorId, cmd.AuthorName),
            new Rating(cmd.Rating),
            cmd.Text);

        await reviews.AddAsync(review, ct);

        logger.LogInformation("Review {ReviewId} created for product {ProductId}, rating {Rating}",
            review.Id, cmd.ProductId, cmd.Rating);

        return review.Id.ToString();
    }
}