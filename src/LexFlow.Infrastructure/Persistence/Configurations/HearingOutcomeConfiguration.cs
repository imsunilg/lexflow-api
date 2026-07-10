using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class HearingOutcomeConfiguration : IEntityTypeConfiguration<HearingOutcome>
{
    public void Configure(EntityTypeBuilder<HearingOutcome> builder)
    {
        builder.ToTable("hearing_outcomes", "legal");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(o => o.HearingId).HasColumnName("hearing_id").IsRequired();
        builder.Property(o => o.Summary).HasColumnName("summary").IsRequired();
        builder.Property(o => o.AdjournReason).HasColumnName("adjourn_reason");
        builder.Property(o => o.RecordedBy).HasColumnName("recorded_by");
        builder.Property(o => o.RecordedAt).HasColumnName("recorded_at").IsRequired();
        builder.Property(o => o.PortalVisible).HasColumnName("portal_visible").IsRequired();

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
