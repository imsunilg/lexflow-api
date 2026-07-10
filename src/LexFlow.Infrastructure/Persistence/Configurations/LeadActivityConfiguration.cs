using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class LeadActivityConfiguration : IEntityTypeConfiguration<LeadActivity>
{
    public void Configure(EntityTypeBuilder<LeadActivity> builder)
    {
        builder.ToTable("lead_activities", "crm");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(a => a.LeadId).HasColumnName("lead_id").IsRequired();
        builder.Property(a => a.ActivityType).HasColumnName("activity_type").IsRequired();
        builder.Property(a => a.Direction).HasColumnName("direction");
        builder.Property(a => a.DurationMin).HasColumnName("duration_min");
        builder.Property(a => a.Subject).HasColumnName("subject");
        builder.Property(a => a.Body).HasColumnName("body");
        builder.Property(a => a.Outcome).HasColumnName("outcome");
        builder.Property(a => a.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(a => a.LoggedBy).HasColumnName("logged_by");

        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");
        builder.Property(a => a.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(a => a.DeletedAt).HasColumnName("deleted_at");
        builder.Property(a => a.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
