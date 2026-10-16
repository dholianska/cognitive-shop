using Ardalis.Specification;
using Catalog.Domain.Entities;

namespace Catalog.Dal.Specifications;

/// <summary>Товари, залишок яких не перевищує поріг (найменший залишок першим).</summary>
public class LowStockProductsSpec : Specification<Product>
{
    public LowStockProductsSpec(int threshold)
    {
        Query.Where(p => p.AvailableStock <= threshold)
             .OrderBy(p => p.AvailableStock)
             .ThenBy(p => p.Name);
    }
}