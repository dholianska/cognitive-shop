using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

/// <summary>Вправа або тест у каталозі (аналог CatalogItem з eShop).</summary>
public class Product
{
    public const int NameMaxLength = 200;
    public const int SkuMaxLength = 50;

    public int Id { get; private set; }
    public string Name { get; private set; } = "";
    public string Sku { get; private set; } = "";
    public decimal Price { get; private set; }
    public int AvailableStock { get; private set; }

    public ProductDetail? Detail { get; private set; }                // 1:1
    public List<ProductImage> Images { get; } = new();                // 1:N
    public List<ProductCategory> ProductCategories { get; } = new();  // M:N

    private Product() { }   // для EF Core

    public Product(string name, string sku, decimal price)
    {
        Name = Guard.Text(name, nameof(Name), NameMaxLength);
        Sku = Guard.Text(sku, nameof(Sku), SkuMaxLength);
        Guard.Positive(price, nameof(Price));
        Price = price;
    }

    public void Rename(string name) => Name = Guard.Text(name, nameof(Name), NameMaxLength);

    public void ChangePrice(decimal newPrice)
    {
        Guard.Positive(newPrice, nameof(Price));
        Price = newPrice;
    }

    public void RemoveStock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity must be positive");
        if (AvailableStock < quantity)
            throw new DomainException($"Only {AvailableStock} units available");

        AvailableStock -= quantity;
    }

    public void AddStock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity must be positive");

        AvailableStock += quantity;
    }

    /// <summary>Створює або оновлює деталі товару (1:1).</summary>
    public void SetDetail(string description, string? manufacturer)
    {
        if (Detail is null)
            Detail = new ProductDetail(description, manufacturer);
        else
            Detail.Update(description, manufacturer);
    }

    public void AddImage(string url, int sortOrder)
    {
        if (Images.Any(i => i.SortOrder == sortOrder))
            throw new DomainException($"Image with sort order {sortOrder} already exists");

        Images.Add(new ProductImage(url, sortOrder));
    }

    /// <summary>Додає товар у категорію. Для повної перевірки дублів завантажуйте ProductCategories.</summary>
    public void AddToCategory(int categoryId)
    {
        if (ProductCategories.Any(pc => pc.CategoryId == categoryId))
            throw new DomainException($"Product is already in category {categoryId}");

        ProductCategories.Add(new ProductCategory(categoryId));
    }
}