using Catalog.Bll.Dtos;

namespace Catalog.Bll.Services;

public interface IProductService
{
    Task<ProductCardDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ProductDto> CreateAsync(CreateProductDto input, CancellationToken ct = default);
    Task ChangePriceAsync(int id, decimal newPrice, CancellationToken ct = default);
    Task AddStockAsync(int id, int quantity, CancellationToken ct = default);
    Task RemoveStockAsync(int id, int quantity, CancellationToken ct = default);
    Task AssignCategoryAsync(int productId, int categoryId, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default);
    Task<CategoryWithProductsDto> GetWithProductsAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<ProductDto>> GetRecentlyAddedAsync(string categoryName, DateTime since, CancellationToken ct = default);
}