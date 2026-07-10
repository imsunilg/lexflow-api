using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class DocumentIndexOutboxConfiguration : IEntityTypeConfiguration<DocumentIndexOutbox>
{
    public void Configure(EntityTypeBuilder<DocumentIndexOutbox> builder)
    {
        builder.ToTable("document_index_outbox", "dms");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(o => o.DocumentId).HasColumnName("document_id").IsRequired();
        builder.Property(o => o.VersionId).HasColumnName("version_id");
        builder.Property(o => o.Operation).HasColumnName("operation").IsRequired();
        builder.Property(o => o.Status).HasColumnName("status").IsRequired();
        builder.Property(o => o.Attempts).HasColumnName("attempts").IsRequired();
        builder.Property(o => o.LastError).HasColumnName("last_error");
        builder.Property(o => o.DispatchedAt).HasColumnName("dispatched_at");
        builder.Property(o => o.ProcessedAt).HasColumnName("processed_at");

        builder.Property(o => o.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(o => o.CreatedBy).HasColumnName("created_by");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");
        builder.Property(o => o.UpdatedBy).HasColumnName("updated_by");
        builder.Property(o => o.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(o => o.DeletedAt).HasColumnName("deleted_at");
        builder.Property(o => o.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(o => !o.IsDeleted);
    }
}
