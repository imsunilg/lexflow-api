using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.ToTable("document_versions", "dms");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id).HasColumnName("id");
        builder.Property(v => v.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(v => v.DocumentId).HasColumnName("document_id").IsRequired();
        builder.Property(v => v.VersionNo).HasColumnName("version_no").IsRequired();
        builder.Property(v => v.BlobPath).HasColumnName("blob_path").IsRequired();
        builder.Property(v => v.SizeBytes).HasColumnName("size_bytes").IsRequired();
        builder.Property(v => v.Mime).HasColumnName("mime");
        builder.Property(v => v.HashSha256).HasColumnName("hash_sha256").IsRequired();
        builder.Property(v => v.OcrStatus).HasColumnName("ocr_status").IsRequired();
        builder.Property(v => v.TextExtracted).HasColumnName("text_extracted").IsRequired();
        builder.Property(v => v.UploadedBy).HasColumnName("uploaded_by");

        builder.Property(v => v.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(v => v.CreatedBy).HasColumnName("created_by");
        builder.Property(v => v.UpdatedAt).HasColumnName("updated_at");
        builder.Property(v => v.UpdatedBy).HasColumnName("updated_by");
        builder.Property(v => v.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(v => v.DeletedAt).HasColumnName("deleted_at");
        builder.Property(v => v.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(v => !v.IsDeleted);
    }
}
