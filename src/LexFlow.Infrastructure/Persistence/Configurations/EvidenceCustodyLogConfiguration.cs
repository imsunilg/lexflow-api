using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class EvidenceCustodyLogConfiguration : IEntityTypeConfiguration<EvidenceCustodyLog>
{
    public void Configure(EntityTypeBuilder<EvidenceCustodyLog> builder)
    {
        builder.ToTable("evidence_custody_log", "legal");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(l => l.EvidenceId).HasColumnName("evidence_id").IsRequired();
        builder.Property(l => l.Action).HasColumnName("action").IsRequired();
        builder.Property(l => l.Holder).HasColumnName("holder");
        builder.Property(l => l.At).HasColumnName("at").IsRequired();
        builder.Property(l => l.Note).HasColumnName("note");

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
