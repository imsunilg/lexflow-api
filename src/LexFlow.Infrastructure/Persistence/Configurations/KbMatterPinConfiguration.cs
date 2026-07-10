using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class KbMatterPinConfiguration : IEntityTypeConfiguration<KbMatterPin>
{
    public void Configure(EntityTypeBuilder<KbMatterPin> builder)
    {
        builder.ToTable("kb_matter_pins", "kb");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(p => p.MatterId).HasColumnName("matter_id").IsRequired();
        builder.Property(p => p.KbRefKind).HasColumnName("kb_ref_kind").IsRequired();
        builder.Property(p => p.KbRefId).HasColumnName("kb_ref_id").IsRequired();
        builder.Property(p => p.Note).HasColumnName("note");
        builder.Property(p => p.SnapshotText).HasColumnName("snapshot_text");
        builder.Property(p => p.PinnedBy).HasColumnName("pinned_by");
        builder.Property(p => p.PinnedAt).HasColumnName("pinned_at").IsRequired();

        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");
        builder.Property(p => p.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(p => p.DeletedAt).HasColumnName("deleted_at");
        builder.Property(p => p.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
