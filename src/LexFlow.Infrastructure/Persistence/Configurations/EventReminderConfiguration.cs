using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class EventReminderConfiguration : IEntityTypeConfiguration<EventReminder>
{
    public void Configure(EntityTypeBuilder<EventReminder> builder)
    {
        builder.ToTable("event_reminders", "ops");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(r => r.EventRefKind).HasColumnName("event_ref_kind").IsRequired();
        builder.Property(r => r.EventRefId).HasColumnName("event_ref_id").IsRequired();
        builder.Property(r => r.OffsetMinutes).HasColumnName("offset_minutes").IsRequired();
        builder.Property(r => r.Channel).HasColumnName("channel").IsRequired();
        builder.Property(r => r.Status).HasColumnName("status").IsRequired();

        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by");
        builder.Property(r => r.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(r => r.DeletedAt).HasColumnName("deleted_at");
        builder.Property(r => r.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
