using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class EvidenceItemConfiguration : IEntityTypeConfiguration<EvidenceItem>
{
    public void Configure(EntityTypeBuilder<EvidenceItem> builder)
    {
        builder.ToTable("evidence_items", "legal");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.CaseId).HasColumnName("case_id").IsRequired();
        builder.Property(e => e.ExhibitNo).HasColumnName("exhibit_no");
        builder.Property(e => e.Kind).HasColumnName("kind").IsRequired();
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.Marked).HasColumnName("marked").IsRequired();
        builder.Property(e => e.Objected).HasColumnName("objected").IsRequired();
        builder.Property(e => e.CustodyStatus).HasColumnName("custody_status");
        builder.Property(e => e.DocumentId).HasColumnName("document_id");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(e => e.DeletedAt).HasColumnName("deleted_at");
        builder.Property(e => e.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
