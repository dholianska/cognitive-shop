using AutoMapper;
using Catalog.Bll.Caching;
using Catalog.Bll.Dtos;
using Catalog.Bll.Events;
using Catalog.Dal.Abstractions;
using Catalog.Dal.Specifications;
using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;
using Catalog.Domain.Queries;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Catalog.Bll.Services;

public sealed class ProductService(IUnitOfWork uow, IMapper mapper, ITwoLevelCache cache,
    IProductEventsOutbox events, ILogger<ProductService> logger)
    : IProductService
{
    private static readonly ActivitySource Source = new("Catalog.Api");
    private const int MaxPageSize = 100;

    /// <summary>
    /// Список з пагінацією, фільтрацією й сортуванням. Усе виконує база (WHERE, ORDER BY,
    /// OFFSET/FETCH, COUNT), у пам'ять потрапляє лише потрібна сторінка.
    /// </summary>
    public async Task<PagedResult<ProductDto>> GetListAsync(ProductQuery query, CancellationToken ct = default)
    {
        // нормалізація: сторінка від 1 (верхня межа захищає від переповнення int у Skip),
        // розмір сторінки в межах 1..100
        var q = query with
        {
            Page = Math.Clamp(query.Page, 1, 1_000_000),
            PageSize = Math.Clamp(query.PageSize, 1, MaxPageSize)
        };

        var total = await uow.Products.CountAsync(new ProductsFilterSpec(q, paginate: false), ct);
        var items = await uow.Products.ListAsync(new ProductsFilterSpec(q), ct);

        return new PagedResult<ProductDto>(mapper.Map<List<ProductDto>>(items), total, q.Page, q.PageSize);
    }

    /// <summary>Друга специфікація з іншими критеріями: той самий ListAsync, без змін у репозиторії.</summary>
    public async Task<IReadOnlyList<ProductDto>> GetLowStockAsync(int threshold, CancellationToken ct = default)
        => mapper.Map<List<ProductDto>>(await uow.Products.ListAsync(new LowStockProductsSpec(threshold), ct));

    public async Task<ProductCardDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var dto = await cache.GetOrCreateAsync(CacheKeys.Product(id), CachePolicy.ProductCard, async c =>
        {
            // Eager Loading: товар, деталі, зображення, категорії
            var product = await uow.Products.GetWithDetailsAsync(id, c);
            return product is null ? null : mapper.Map<ProductCardDto>(product);
        }, ct);

        return dto ?? throw new EntityNotFoundException($"Product {id} not found");
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

        // Id товару видає база, тому подія створюється після першого збереження.
        // Явна транзакція: товар і подія в outbox зберігаються разом або не зберігаються взагалі
        await uow.BeginTransactionAsync(ct);
        await uow.Products.AddAsync(product, ct);
        await uow.SaveChangesAsync(ct);
        await events.EnqueueProductCreatedAsync(product.Id, product.Name, product.Sku, product.Price,
            product.AvailableStock, ct);
        await uow.SaveChangesAsync(ct);
        await uow.CommitTransactionAsync(ct);

        logger.LogInformation("Product {ProductId} created with SKU {Sku}", product.Id, product.Sku);
        return mapper.Map<ProductDto>(product);
    }

    public async Task ChangePriceAsync(int id, decimal newPrice, CancellationToken ct = default)
    {
        using var activity = Source.StartActivity("ChangePrice");
        activity?.SetTag("product.id", id);

        var product = await GetTrackedAsync(id, ct);
        var oldPrice = product.Price;

        product.ChangePrice(newPrice);         // інваріант у сутності
        await events.EnqueuePriceChangedAsync(id, product.Name, oldPrice, newPrice, ct);
        await uow.SaveChangesAsync(ct);         // зміна ціни і подія в outbox зберігаються атомарно
        await InvalidateAsync(id, "PriceChanged", ct);

        logger.LogInformation("Product {ProductId} price changed from {OldPrice} to {NewPrice}",
            id, oldPrice, newPrice);
    }

    public async Task RenameAsync(int id, string name, CancellationToken ct = default)
    {
        var product = await GetTrackedAsync(id, ct);
        var oldName = product.Name;

        product.Rename(name);                       // інваріант у сутності (порожня назва, довжина)
        if (product.Name == oldName) return;        // назва не змінилась: ні запису, ні події

        await events.EnqueueRenamedAsync(id, oldName, product.Name, product.Price, ct);
        await uow.SaveChangesAsync(ct);             // нова назва і подія в outbox зберігаються атомарно
        await InvalidateAsync(id, "ProductRenamed", ct);

        logger.LogInformation("Product {ProductId} renamed from {OldName} to {NewName}", id, oldName, product.Name);
    }

    public async Task AddStockAsync(int id, int quantity, CancellationToken ct = default)
    {
        var product = await GetTrackedAsync(id, ct);
        product.AddStock(quantity);
        await uow.SaveChangesAsync(ct);
        await InvalidateAsync(id, "StockAdded", ct);
    }

    public async Task RemoveStockAsync(int id, int quantity, CancellationToken ct = default)
    {
        var product = await GetTrackedAsync(id, ct);
        product.RemoveStock(quantity);          // нестача товару: DomainException (409)
        await uow.SaveChangesAsync(ct);
        await InvalidateAsync(id, "StockRemoved", ct);
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
        await InvalidateAsync(productId, "CategoryAssigned", ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var product = await GetTrackedAsync(id, ct);
        var name = product.Name;

        uow.Products.Delete(product);           // деталі, зображення й зв'язки з категоріями видалить каскад
        await events.EnqueueDeletedAsync(id, name, ct);
        await uow.SaveChangesAsync(ct);         // видалення і подія в outbox зберігаються атомарно
        await InvalidateAsync(id, "ProductDeleted", ct);

        logger.LogInformation("Product {ProductId} deleted", id);
    }

    private async Task<Product> GetTrackedAsync(int id, CancellationToken ct)
        => await uow.Products.GetByIdAsync(id, ct)
           ?? throw new EntityNotFoundException($"Product {id} not found");

    private Task InvalidateAsync(int id, string reason, CancellationToken ct)
        => cache.RemoveAsync(CacheKeys.Product(id), reason, ct);
}