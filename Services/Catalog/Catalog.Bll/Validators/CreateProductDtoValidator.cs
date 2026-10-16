using Catalog.Bll.Dtos;
using Catalog.Domain.Entities;
using FluentValidation;

namespace Catalog.Bll.Validators;

/// <summary>Правила для створення товару. Максимальні довжини беруться з сутностей (одне джерело правди).</summary>
public class CreateProductDtoValidator : AbstractValidator<CreateProductDto>
{
    public CreateProductDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Product.NameMaxLength);

        RuleFor(x => x.Sku)
            .NotEmpty()
            .MaximumLength(Product.SkuMaxLength)
            .Matches("^[A-Z0-9-]+$")
            .WithMessage("Sku may contain only capital letters, digits and hyphens");

        RuleFor(x => x.Price)
            .GreaterThan(0)
            .LessThanOrEqualTo(1_000_000m);

        RuleFor(x => x.Description).MaximumLength(ProductDetail.DescriptionMaxLength);
        RuleFor(x => x.Manufacturer).MaximumLength(ProductDetail.ManufacturerMaxLength);
    }
}