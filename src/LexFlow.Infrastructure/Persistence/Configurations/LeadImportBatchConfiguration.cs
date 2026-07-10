using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class LeadImportBatchConfiguration : IEntityTypeConfiguration<LeadImportBatch>
{
    public void Configure(EntityTypeBuilder<LeadImportBatch> builder)
    {
        builder.ToTable("lead_import_batches", "crm");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id).HasColumnName("id");
        builder.Property(b => b.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(b => b.FileName).HasColumnName("file_name").IsRequired();
        builder.Property(b => b.SourceBlobPath).HasColumnName("source_blob_path").IsRequired();
        builder.Property(b => b.Status).HasColumnName("status").IsRequired();
        builder.Property(b => b.TotalRows).HasColumnName("total_rows").IsRequired();
        builder.Property(b => b.SuccessCount).HasColumnName("success_count").IsRequired();
        builder.Property(b => b.ErrorCount).HasColumnName("error_count").IsRequired();
        builder.Property(b => b.ErrorFileBlobPath).HasColumnName("error_file_blob_path");
        builder.Property(b => b.StartedAt).HasColumnName("started_at");
        builder.Property(b => b.CompletedAt).HasColumnName("completed_at");
        builder.Property(b => b.FailureReason).HasColumnName("failure_reason");

        builder.Property(b => b.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(b => b.CreatedBy).HasColumnName("created_by");
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at");
        builder.Property(b => b.UpdatedBy).HasColumnName("updated_by");
        builder.Property(b => b.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(b => b.DeletedAt).HasColumnName("deleted_at");
        builder.Property(b => b.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(b => !b.IsDeleted);
    }
}
