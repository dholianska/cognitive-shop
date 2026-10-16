using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.ToTable("ProductCategories");

        builder.HasKey(pc => new { pc.ProductId, pc.CategoryId });   // складений ключ
        builder.Property(pc => pc.AddedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(pc => pc.Product).WithMany(p => p.ProductCategories)
            .HasForeignKey(pc => pc.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pc => pc.Category).WithMany(c => c.ProductCategories)
            .HasForeignKey(pc => pc.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // запит "товари категорії, нові зверху"
        builder.HasIndex(pc => new { pc.CategoryId, pc.AddedAt }).IsDescending(false, true);
    }
}