using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImages");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Url).IsRequired().HasMaxLength(ProductImage.UrlMaxLength);
        builder.Property(i => i.SortOrder).HasDefaultValue(0);

        // в одного товару не може бути двох зображень з однаковим порядком показу
        builder.HasIndex(i => new { i.ProductId, i.SortOrder }).IsUnique();
    }
}