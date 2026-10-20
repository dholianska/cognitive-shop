using FluentValidation;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Commands.CreateReview;

public class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewCommandValidator(IReviewRepository reviews)
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.ProductName).NotEmpty().MaximumLength(Review.ProductNameMaxLength);
        RuleFor(x => x.AuthorId).NotEmpty().MaximumLength(Author.UserIdMaxLength);
        RuleFor(x => x.AuthorName).NotEmpty().MaximumLength(Author.DisplayNameMaxLength);
        RuleFor(x => x.Rating).InclusiveBetween(Rating.Min, Rating.Max);
        RuleFor(x => x.Text).NotEmpty().MaximumLength(Review.TextMaxLength);

        // асинхронне правило із запитом до MongoDB: один відгук від автора на товар.
        // Запит у базу йде лише тоді, коли два потрібні поля вже коректні.
        RuleFor(x => x)
            .MustAsync(async (cmd, ct) => !await reviews.ExistsAsync(cmd.ProductId, cmd.AuthorId.Trim(), ct))
            .WithMessage("You have already reviewed this product")
            .OverridePropertyName(nameof(CreateReviewCommand.AuthorId))
            .When(x => x.ProductId > 0 && !string.IsNullOrWhiteSpace(x.AuthorId));
    }
}