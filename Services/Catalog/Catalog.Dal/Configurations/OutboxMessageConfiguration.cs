using Catalog.Dal.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.EventType).IsRequired().HasMaxLength(200);
        builder.Property(m => m.Exchange).IsRequired().HasMaxLength(200);
        builder.Property(m => m.RoutingKey).IsRequired().HasMaxLength(200);
        builder.Property(m => m.Payload).IsRequired();                    // nvarchar(max)
        builder.Property(m => m.CorrelationId).HasMaxLength(100);
        builder.Property(m => m.TraceParent).HasMaxLength(100);
        builder.Property(m => m.LastError).HasMaxLength(2000);

        builder.HasIndex(m => m.EventId).IsUnique();                      // подія потрапляє в outbox лише раз
        builder.HasIndex(m => m.ProcessedAt);                             // швидкий пошук необроблених
    }
}