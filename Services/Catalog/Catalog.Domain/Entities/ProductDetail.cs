namespace Catalog.Domain.Entities;

public class ProductDetail
{
    public const int DescriptionMaxLength = 2000;
    public const int ManufacturerMaxLength = 200;

    public int ProductId { get; private set; }          // PK і FK одночасно
    public string Description { get; private set; } = "";
    public string? Manufacturer { get; private set; }

    private ProductDetail() { }   // для EF Core

    public ProductDetail(string description, string? manufacturer) => Update(description, manufacturer);

    public void Update(string description, string? manufacturer)
    {
        Description = Guard.Text(description, nameof(Description), DescriptionMaxLength);
        Manufacturer = string.IsNullOrWhiteSpace(manufacturer)
            ? null
            : Guard.Text(manufacturer, nameof(Manufacturer), ManufacturerMaxLength);
    }
}