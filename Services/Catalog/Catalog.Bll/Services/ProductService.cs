using AutoMapper;
using Catalog.Bll.Dtos;
using Catalog.Dal.Abstractions;
using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Catalog.Bll.Services;

public sealed class ProductService(IUnitOfWork uow, IMapper mapper, ILogger<ProductService> logger)
    : IProductService
{
    public async Task<ProductCardDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        // Eager Loading: товар, деталі, зображення й категорії одним запитом
        var product = await uow.Products.GetWithDetailsAsync(id, ct)
                      ?? throw new EntityNotFoundException($"Product {id} not found");
        return mapper.Map<ProductCardDto>(product);
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto input, CancellationToken ct = default)
    {
        var sku = input.Sku.Trim();

        // правило, якому потрібні дані з БД, перевіряє сервіс (а не сутність)
        if (await uow.Products.ExistsBySkuAsync(sku, ct))
            throw new ConflictException($"Product with SKU '{sku}' already exists");

        // правила самої сутності (ціна більша за 0, довжини) перевіряє конструктор
        var product = new Product(input.Name, sku, input.Price);
        if (!string.IsNullOrWhiteSpace(input.Description))
            product.SetDetail(input.Description, input.Manufacturer);

        await uow.Products.AddAsync(product, ct);
        await uow.SaveChangesAsync(ct);   // один виклик наприкінці операції

        logger.LogInformation("Product {ProductId} created with SKU {Sku}", product.Id, product.Sku);
        return mapper.Map<ProductDto>(product);
    }

    public async Task ChangePriceAsync(int id, decimal newPrice, CancellationToken ct = default)
    {
        var product = await GetTrackedAsync(id, ct);
        var oldPrice = product.Price;

        product.ChangePrice(newPrice);          // інваріант у сутності
        await uow.SaveChangesAsync(ct);         // контекст сам помітив зміну: Update() не потрібен

        logger.LogInformation("Product {ProductId} price changed from {OldPrice} to {NewPrice}",
            id, oldPrice, newPrice);
    }

    public async Task AddStockAsync(int id, int quantity, CancellationToken ct = default)
    {
        var product = await GetTrackedAsync(id, ct);
        product.AddStock(quantity);
        await uow.SaveChangesAsync(ct);
    }

    public async Task RemoveStockAsync(int id, int quantity, CancellationToken ct = default)
    {
        var product = await GetTrackedAsync(id, ct);
        product.RemoveStock(quantity);          // нестача товару: DomainException (409)
        await uow.SaveChangesAsync(ct);
    }

    /// <summary>M:N: додає товар у категорію (дату AddedAt проставляє база).</summary>
    public async Task AssignCategoryAsync(int productId, int categoryId, CancellationToken ct = default)
    {
        var product = await uow.Products.GetWithDetailsAsync(productId, ct)   // з уже завантаженими категоріями
                      ?? throw new EntityNotFoundException($"Product {productId} not found");

        _ = await uow.Categories.GetByIdAsync(categoryId, ct)
            ?? throw new EntityNotFoundException($"Category {categoryId} not found");

        product.AddToCategory(categoryId);      // дубль: DomainException (409)
        await uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var product = await GetTrackedAsync(id, ct);
        uow.Products.Delete(product);           // деталі, зображення й зв'язки з категоріями видалить каскад
        await uow.SaveChangesAsync(ct);

        logger.LogInformation("Product {ProductId} deleted", id);
    }

    private async Task<Product> GetTrackedAsync(int id, CancellationToken ct)
        => await uow.Products.GetByIdAsync(id, ct)
           ?? throw new EntityNotFoundException($"Product {id} not found");
}