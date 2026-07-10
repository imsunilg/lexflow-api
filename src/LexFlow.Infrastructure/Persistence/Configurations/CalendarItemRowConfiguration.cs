using LexFlow.Infrastructure.Ops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

/// <summary>Maps to the read-only ops.v_calendar_items view — see CalendarItemRow for why this is keyless and Postgres-only.</summary>
public sealed class CalendarItemRowConfiguration : IEntityTypeConfiguration<CalendarItemRow>
{
    public void Configure(EntityTypeBuilder<CalendarItemRow> builder)
    {
        builder.ToView("v_calendar_items", "ops");
        builder.HasNoKey();

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.TenantId).HasColumnName("tenant_id");
        builder.Property(r => r.ItemKind).HasColumnName("item_kind");
        builder.Property(r => r.Title).HasColumnName("title");
        builder.Property(r => r.StartsAt).HasColumnName("starts_at");
        builder.Property(r => r.EndsAt).HasColumnName("ends_at");
        builder.Property(r => r.AllDay).HasColumnName("all_day");
        builder.Property(r => r.MatterId).HasColumnName("matter_id");
        builder.Property(r => r.Location).HasColumnName("location");
        builder.Property(r => r.Status).HasColumnName("status");
        builder.Property(r => r.IsLocked).HasColumnName("is_locked");
    }
}
