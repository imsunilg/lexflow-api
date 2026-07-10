using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

/// <summary>ops.reminder_dispatch_log is PARTITION BY RANGE(sent_at) with PK (id, sent_at) — the composite key here mirrors that (a partitioned table's primary key must include the partition key).</summary>
public sealed class ReminderDispatchLogConfiguration : IEntityTypeConfiguration<ReminderDispatchLog>
{
    public void Configure(EntityTypeBuilder<ReminderDispatchLog> builder)
    {
        builder.ToTable("reminder_dispatch_log", "ops");
        builder.HasKey(l => new { l.Id, l.SentAt });

        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(l => l.ReminderId).HasColumnName("reminder_id").IsRequired();
        builder.Property(l => l.Channel).HasColumnName("channel").IsRequired();
        builder.Property(l => l.SentAt).HasColumnName("sent_at").IsRequired();
        builder.Property(l => l.Status).HasColumnName("status").IsRequired();
        builder.Property(l => l.ProviderRef).HasColumnName("provider_ref");
        builder.Property(l => l.Error).HasColumnName("error");

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
