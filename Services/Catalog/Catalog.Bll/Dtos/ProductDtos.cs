using System.ComponentModel.DataAnnotations;

namespace Catalog.Bll.Dtos;

// ---- вихідні DTO ----
public record ProductDto(int Id, string Name, string Sku, decimal Price, int AvailableStock);

public record ProductImageDto(string Url, int SortOrder);

/// <summary>Повна картка товару: деталі, зображення й категорії (Eager Loading).</summary>
public class ProductCardDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Sku { get; set; } = "";
    public decimal Price { get; set; }
    public int AvailableStock { get; set; }
    public string? Description { get; set; }
    public string? Manufacturer { get; set; }
    public List<ProductImageDto> Images { get; set; } = new();
    public List<string> Categories { get; set; } = new();
}

// ---- вхідні DTO ----
// CreateProductDto перевіряє FluentValidation (крок 6), тому атрибутів тут немає.
public record CreateProductDto(
    string Name, string Sku, decimal Price,
    string? Description = null, string? Manufacturer = null);

// прості правила перевіряє DataAnnotations (до виклику сервісу)
public class ChangePriceDto
{
    [Range(typeof(decimal), "0.01", "1000000")]
    public decimal NewPrice { get; set; }
}

public class StockQuantityDto
{
    [Range(1, 100000)]
    public int Quantity { get; set; }
}