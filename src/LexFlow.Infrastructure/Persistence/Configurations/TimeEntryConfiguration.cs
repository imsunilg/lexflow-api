using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
{
    public void Configure(EntityTypeBuilder<TimeEntry> builder)
    {
        builder.ToTable("time_entries", "fin");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(t => t.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(t => t.MatterId).HasColumnName("matter_id").IsRequired();
        builder.Property(t => t.ActivityCodeId).HasColumnName("activity_code_id");
        builder.Property(t => t.EntryDate).HasColumnName("entry_date").IsRequired();
        builder.Property(t => t.StartedAt).HasColumnName("started_at");
        builder.Property(t => t.DurationMin).HasColumnName("duration_min").IsRequired();
        builder.Property(t => t.RoundedMin).HasColumnName("rounded_min").IsRequired();
        builder.Property(t => t.Billable).HasColumnName("billable").IsRequired();
        builder.Property(t => t.Narrative).HasColumnName("narrative");
        builder.Property(t => t.InternalNote).HasColumnName("internal_note");
        builder.Property(t => t.Status).HasColumnName("status").IsRequired();
        builder.Property(t => t.RateSnapshot).HasColumnName("rate_snapshot").HasColumnType("numeric(18,4)");
        builder.Property(t => t.AmountSnapshot).HasColumnName("amount_snapshot").HasColumnType("numeric(18,2)");
        builder.Property(t => t.InvoiceLineId).HasColumnName("invoice_line_id");
        builder.Property(t => t.Source).HasColumnName("source").IsRequired();
        builder.Property(t => t.ApprovedBy).HasColumnName("approved_by");
        builder.Property(t => t.ApprovedAt).HasColumnName("approved_at");

        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.CreatedBy).HasColumnName("created_by");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by");
        builder.Property(t => t.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(t => t.DeletedAt).HasColumnName("deleted_at");
        builder.Property(t => t.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
