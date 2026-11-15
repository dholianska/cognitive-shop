using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Catalog.Dal.Outbox;

namespace Catalog.Dal;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ProductDetail> ProductDetails => Set<ProductDetail>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    // усі IEntityTypeConfiguration<T> із цієї збірки підключаються автоматично й централізовано
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}