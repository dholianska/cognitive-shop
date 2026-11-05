using FluentValidation;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Entities;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Commands.AddComment;

public class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentCommandValidator()
    {
        RuleFor(x => x.ReviewId).MustBeObjectId();
        RuleFor(x => x.AuthorId).NotEmpty().MaximumLength(Author.UserIdMaxLength);
        RuleFor(x => x.AuthorName).NotEmpty().MaximumLength(Author.DisplayNameMaxLength);
        RuleFor(x => x.Text).NotEmpty().MaximumLength(Comment.TextMaxLength);
    }
}