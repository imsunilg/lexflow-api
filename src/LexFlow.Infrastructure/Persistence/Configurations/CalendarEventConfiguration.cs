using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    public void Configure(EntityTypeBuilder<CalendarEvent> builder)
    {
        builder.ToTable("calendar_events", "ops");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.Kind).HasColumnName("kind").IsRequired();
        builder.Property(e => e.Title).HasColumnName("title").IsRequired();
        builder.Property(e => e.StartsAt).HasColumnName("starts_at").IsRequired();
        builder.Property(e => e.EndsAt).HasColumnName("ends_at").IsRequired();
        builder.Property(e => e.AllDay).HasColumnName("all_day").IsRequired();
        builder.Property(e => e.Location).HasColumnName("location");
        builder.Property(e => e.VideoLink).HasColumnName("video_link");
        builder.Property(e => e.MatterId).HasColumnName("matter_id");
        builder.Property(e => e.Rrule).HasColumnName("rrule");
        builder.Property(e => e.SeriesId).HasColumnName("series_id");
        builder.Property(e => e.OrganizerId).HasColumnName("organizer_id");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(e => e.DeletedAt).HasColumnName("deleted_at");
        builder.Property(e => e.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
