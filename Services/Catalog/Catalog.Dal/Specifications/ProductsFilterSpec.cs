using Ardalis.Specification;
using Catalog.Domain.Entities;
using Catalog.Domain.Queries;

namespace Catalog.Dal.Specifications;

/// <summary>
/// Специфікація: критерії вибірки товарів (фільтри, сортування, сторінка) окремо від репозиторію.
/// Не залежить від EF Core, тому її можна тестувати без бази.
/// paginate = false: лише фільтри (для підрахунку загальної кількості без Skip/Take).
/// </summary>
public class ProductsFilterSpec : Specification<Product>
{
    public ProductsFilterSpec(ProductQuery q, bool paginate = true)
    {
        // значення беремо в локальні змінні: EF перетворить їх на параметри SQL-запиту
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var search = q.Search.Trim();
            Query.Where(p => p.Name.Contains(search));
        }

        if (q.MinPrice is not null)
        {
            var min = q.MinPrice.Value;
            Query.Where(p => p.Price >= min);
        }

        if (q.MaxPrice is not null)
        {
            var max = q.MaxPrice.Value;
            Query.Where(p => p.Price <= max);
        }

        if (q.CategoryId is not null)
        {
            var categoryId = q.CategoryId.Value;   // фільтр через зв'язок M:N, теж у SQL
            Query.Where(p => p.ProductCategories.Any(pc => pc.CategoryId == categoryId));
        }

        if (!paginate) return;

        // сортування лише за дозволеними полями; Id другим ключем робить порядок стабільним,
        // тому сторінки не "перемішуються" між запитами
        var desc = string.Equals(q.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        switch (q.SortBy?.ToLowerInvariant())
        {
            case "price":
                if (desc) Query.OrderByDescending(p => p.Price).ThenBy(p => p.Id);
                else Query.OrderBy(p => p.Price).ThenBy(p => p.Id);
                break;
            case "stock":
                if (desc) Query.OrderByDescending(p => p.AvailableStock).ThenBy(p => p.Id);
                else Query.OrderBy(p => p.AvailableStock).ThenBy(p => p.Id);
                break;
            default:
                if (desc) Query.OrderByDescending(p => p.Name).ThenBy(p => p.Id);
                else Query.OrderBy(p => p.Name).ThenBy(p => p.Id);
                break;
        }

        Query.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize);   // SQL: OFFSET ... FETCH
    }
}