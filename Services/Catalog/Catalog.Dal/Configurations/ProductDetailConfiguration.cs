using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class ProductDetailConfiguration : IEntityTypeConfiguration<ProductDetail>
{
    public void Configure(EntityTypeBuilder<ProductDetail> builder)
    {
        builder.ToTable("ProductDetails");
        builder.HasKey(d => d.ProductId);   // 1:1: первинний ключ є водночас зовнішнім
        builder.Property(d => d.Description).IsRequired().HasMaxLength(ProductDetail.DescriptionMaxLength);
        builder.Property(d => d.Manufacturer).HasMaxLength(ProductDetail.ManufacturerMaxLength);
    }
}