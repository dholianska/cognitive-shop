using Catalog.Domain.Queries;
using FluentValidation;

namespace Catalog.Bll.Validators;

/// <summary>
/// Правила для параметрів списку. Номер і розмір сторінки не відхиляються, а нормалізуються
/// в сервісі (сторінка від 1, розмір 1..100); тут перевіряємо логіку фільтрів і сортування.
/// </summary>
public class ProductQueryValidator : AbstractValidator<ProductQuery>
{
    private static readonly string[] SortFields = { "name", "price", "stock" };
    private static readonly string[] SortDirections = { "asc", "desc" };

    public ProductQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.MinPrice).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.CategoryId).GreaterThan(0);

        // правило між двома полями; помилку прив'язуємо до поля MinPrice
        RuleFor(x => x)
            .Must(x => x.MinPrice is null || x.MaxPrice is null || x.MinPrice <= x.MaxPrice)
            .OverridePropertyName(nameof(ProductQuery.MinPrice))
            .WithMessage("MinPrice must not be greater than MaxPrice");

        RuleFor(x => x.SortBy)
            .Must(v => v is not null && SortFields.Contains(v.ToLowerInvariant()))
            .WithMessage("SortBy must be one of: name, price, stock");

        RuleFor(x => x.SortDir)
            .Must(v => v is not null && SortDirections.Contains(v.ToLowerInvariant()))
            .WithMessage("SortDir must be 'asc' or 'desc'");
    }
}