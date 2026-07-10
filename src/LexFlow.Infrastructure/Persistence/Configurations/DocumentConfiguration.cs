using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents", "dms");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(d => d.FolderId).HasColumnName("folder_id");
        builder.Property(d => d.MatterId).HasColumnName("matter_id");
        builder.Property(d => d.ClientId).HasColumnName("client_id");
        builder.Property(d => d.CaseId).HasColumnName("case_id");
        builder.Property(d => d.Title).HasColumnName("title").IsRequired();
        builder.Property(d => d.DocType).HasColumnName("doc_type").IsRequired();
        builder.Property(d => d.Confidentiality).HasColumnName("confidentiality").IsRequired();
        builder.Property(d => d.CurrentVersionId).HasColumnName("current_version_id");
        builder.Property(d => d.PortalPublished).HasColumnName("portal_published").IsRequired();

        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(d => d.CreatedBy).HasColumnName("created_by");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");
        builder.Property(d => d.UpdatedBy).HasColumnName("updated_by");
        builder.Property(d => d.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(d => d.DeletedAt).HasColumnName("deleted_at");
        builder.Property(d => d.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}
