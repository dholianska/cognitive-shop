namespace Catalog.Domain.Entities;

public class Category
{
    public const int NameMaxLength = 100;

    public int Id { get; private set; }
    public string Name { get; private set; } = "";

    public List<ProductCategory> ProductCategories { get; } = new();

    private Category() { }   // для EF Core

    public Category(string name) => Name = Guard.Text(name, nameof(Name), NameMaxLength);

    public void Rename(string name) => Name = Guard.Text(name, nameof(Name), NameMaxLength);
}