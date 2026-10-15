using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

/// <summary>Проміжна сутність M:N із власним полем AddedAt (автоматичне M:N такого поля не дозволило б).</summary>
public class ProductCategory
{
    public int ProductId { get; private set; }
    public Product Product { get; private set; } = null!;

    public int CategoryId { get; private set; }
    public Category Category { get; private set; } = null!;

    /// <summary>Дата додавання в категорію: її задає база (DEFAULT SYSUTCDATETIME()).</summary>
    public DateTime AddedAt { get; private set; }

    private ProductCategory() { }   // для EF Core

    public ProductCategory(int categoryId)
    {
        if (categoryId <= 0)
            throw new DomainException("CategoryId must be positive");

        CategoryId = categoryId;
    }
}