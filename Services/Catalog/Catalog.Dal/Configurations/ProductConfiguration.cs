using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", t =>
        {
            t.HasCheckConstraint("CK_Products_Price", "[Price] > 0");
            t.HasCheckConstraint("CK_Products_Stock", "[AvailableStock] >= 0");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(Product.NameMaxLength);
        builder.Property(p => p.Sku).IsRequired().HasMaxLength(Product.SkuMaxLength);
        builder.Property(p => p.Price).HasColumnType("decimal(18,2)");
        builder.Property(p => p.AvailableStock).HasDefaultValue(0);

        builder.HasIndex(p => p.Sku).IsUnique();   // артикул унікальний
        builder.HasIndex(p => p.Name);             // пошук і сортування за назвою

        builder.HasOne(p => p.Detail)              // 1:1
            .WithOne()
            .HasForeignKey<ProductDetail>(d => d.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Images)             // 1:N
            .WithOne()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}