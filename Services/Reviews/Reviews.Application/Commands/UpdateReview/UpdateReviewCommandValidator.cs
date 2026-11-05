using FluentValidation;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Entities;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Commands.UpdateReview;

public class UpdateReviewCommandValidator : AbstractValidator<UpdateReviewCommand>
{
    public UpdateReviewCommandValidator()
    {
        RuleFor(x => x.Id).MustBeObjectId();
        RuleFor(x => x.Rating).InclusiveBetween(Rating.Min, Rating.Max);
        RuleFor(x => x.Text).NotEmpty().MaximumLength(Review.TextMaxLength);
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0).When(x => x.ExpectedVersion.HasValue);
    }
}