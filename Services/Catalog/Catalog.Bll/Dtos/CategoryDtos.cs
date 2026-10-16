namespace Catalog.Bll.Dtos;

public record CategoryDto(int Id, string Name);

/// <summary>Категорія з товарами (Explicit Loading).</summary>
public class CategoryWithProductsDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<ProductDto> Products { get; set; } = new();
}