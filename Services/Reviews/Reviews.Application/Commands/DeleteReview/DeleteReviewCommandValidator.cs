using FluentValidation;
using Reviews.Application.Common.Validation;

namespace Reviews.Application.Commands.DeleteReview;

public class DeleteReviewCommandValidator : AbstractValidator<DeleteReviewCommand>
{
    public DeleteReviewCommandValidator() => RuleFor(x => x.Id).MustBeObjectId();
}