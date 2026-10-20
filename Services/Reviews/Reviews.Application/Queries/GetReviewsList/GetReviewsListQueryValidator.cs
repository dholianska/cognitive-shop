using FluentValidation;

namespace Reviews.Application.Queries.GetReviewsList;

public class GetReviewsListQueryValidator : AbstractValidator<GetReviewsListQuery>
{
    // номер ≥ розм≥р стор≥нки не в≥дхил€ютьс€, а нормал≥зуютьс€ в обробнику (стор≥нка в≥д 1, розм≥р 1..100)
    public GetReviewsListQueryValidator()
        => RuleFor(x => x.ProductId).GreaterThan(0).When(x => x.ProductId.HasValue);
}