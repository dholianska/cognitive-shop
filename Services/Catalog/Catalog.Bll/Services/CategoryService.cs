using AutoMapper;
using Catalog.Bll.Dtos;
using Catalog.Dal.Abstractions;
using Catalog.Domain.Exceptions;
using Catalog.Bll.Caching;

namespace Catalog.Bll.Services;

public sealed class CategoryService(IUnitOfWork uow, IMapper mapper, ITwoLevelCache cache) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default)
        => await cache.GetOrCreateAsync<IReadOnlyList<CategoryDto>>(
               CacheKeys.Categories, CachePolicy.Categories,
               async c => mapper.Map<List<CategoryDto>>(await uow.Categories.GetAllAsync(c)), ct) ?? [];

    public async Task<CategoryWithProductsDto> GetWithProductsAsync(int id, CancellationToken ct = default)
    {
        // Explicit Loading: категорія одним запитом, її товари довантажуються другим
        var category = await uow.Categories.GetWithProductsAsync(id, ct)
                       ?? throw new EntityNotFoundException($"Category {id} not found");
        return mapper.Map<CategoryWithProductsDto>(category);
    }

    public async Task<IReadOnlyList<ProductDto>> GetRecentlyAddedAsync(
        string categoryName, DateTime since, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
            throw new ValidationException("Category name is required");

        // LINQ to Entities через проміжну сутність M:N
        var products = await uow.ProductCategories.GetRecentlyAddedAsync(categoryName.Trim(), since, ct);
        return mapper.Map<List<ProductDto>>(products);
    }
}