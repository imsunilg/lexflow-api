using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class HearingConfiguration : IEntityTypeConfiguration<Hearing>
{
    public void Configure(EntityTypeBuilder<Hearing> builder)
    {
        builder.ToTable("hearings", "legal");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id).HasColumnName("id");
        builder.Property(h => h.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(h => h.CaseId).HasColumnName("case_id").IsRequired();
        builder.Property(h => h.Date).HasColumnName("date").IsRequired();
        builder.Property(h => h.Time).HasColumnName("time");
        builder.Property(h => h.CourtTz).HasColumnName("court_tz").IsRequired();
        builder.Property(h => h.Purpose).HasColumnName("purpose");
        builder.Property(h => h.Courtroom).HasColumnName("courtroom");
        builder.Property(h => h.AssignedLawyerId).HasColumnName("assigned_lawyer_id");
        builder.Property(h => h.Status).HasColumnName("status").IsRequired();
        builder.Property(h => h.PortalVisible).HasColumnName("portal_visible").IsRequired();

        builder.Property(h => h.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(h => h.CreatedBy).HasColumnName("created_by");
        builder.Property(h => h.UpdatedAt).HasColumnName("updated_at");
        builder.Property(h => h.UpdatedBy).HasColumnName("updated_by");
        builder.Property(h => h.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(h => h.DeletedAt).HasColumnName("deleted_at");
        builder.Property(h => h.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(h => !h.IsDeleted);
    }
}
