using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class DunningEventConfiguration : IEntityTypeConfiguration<DunningEvent>
{
    public void Configure(EntityTypeBuilder<DunningEvent> builder)
    {
        builder.ToTable("dunning_events", "fin");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(d => d.InvoiceId).HasColumnName("invoice_id").IsRequired();
        builder.Property(d => d.ScheduleId).HasColumnName("schedule_id");
        builder.Property(d => d.StepLabel).HasColumnName("step_label").IsRequired();
        builder.Property(d => d.Channel).HasColumnName("channel").IsRequired();
        builder.Property(d => d.ScheduledFor).HasColumnName("scheduled_for").IsRequired();
        builder.Property(d => d.SentAt).HasColumnName("sent_at");
        builder.Property(d => d.Status).HasColumnName("status").IsRequired();
        builder.Property(d => d.Muted).HasColumnName("muted").IsRequired();

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
