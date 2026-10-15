using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(Category.NameMaxLength);
        builder.HasIndex(c => c.Name).IsUnique();

        // статичний сідінг довідника з фіксованими Id (анонімні типи, бо сеттери приватні)
        builder.HasData(
            new { Id = 1, Name = "Увага" },
            new { Id = 2, Name = "Пам'ять" },
            new { Id = 3, Name = "Швидкість реакції" },
            new { Id = 4, Name = "Просторове мислення" },
            new { Id = 5, Name = "Фокус і продуктивність" });
    }
}