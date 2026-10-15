using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

public class ProductImage
{
    public const int UrlMaxLength = 500;

    public int Id { get; private set; }
    public int ProductId { get; private set; }
    public string Url { get; private set; } = "";
    public int SortOrder { get; private set; }

    private ProductImage() { }   // для EF Core

    public ProductImage(string url, int sortOrder)
    {
        if (sortOrder < 0)
            throw new DomainException("SortOrder must not be negative");

        Url = Guard.Text(url, nameof(Url), UrlMaxLength);
        SortOrder = sortOrder;
    }
}