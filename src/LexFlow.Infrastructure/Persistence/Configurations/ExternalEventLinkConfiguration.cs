using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class ExternalEventLinkConfiguration : IEntityTypeConfiguration<ExternalEventLink>
{
    public void Configure(EntityTypeBuilder<ExternalEventLink> builder)
    {
        builder.ToTable("external_event_links", "ops");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(l => l.EventId).HasColumnName("event_id").IsRequired();
        builder.Property(l => l.ExternalAccountId).HasColumnName("external_account_id").IsRequired();
        builder.Property(l => l.ExternalEventId).HasColumnName("external_event_id").IsRequired();
        builder.Property(l => l.Etag).HasColumnName("etag");
        builder.Property(l => l.LastSyncedAt).HasColumnName("last_synced_at");

        builder.Property(l => l.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(l => l.CreatedBy).HasColumnName("created_by");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");
        builder.Property(l => l.UpdatedBy).HasColumnName("updated_by");
        builder.Property(l => l.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(l => l.DeletedAt).HasColumnName("deleted_at");
        builder.Property(l => l.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(l => !l.IsDeleted);
    }
}
