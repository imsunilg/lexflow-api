using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class RecurrenceExceptionConfiguration : IEntityTypeConfiguration<RecurrenceException>
{
    public void Configure(EntityTypeBuilder<RecurrenceException> builder)
    {
        builder.ToTable("recurrence_exceptions", "ops");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(r => r.EventId).HasColumnName("event_id").IsRequired();
        builder.Property(r => r.OccurrenceDate).HasColumnName("occurrence_date").IsRequired();
        builder.Property(r => r.ExceptionType).HasColumnName("exception_type").IsRequired();
        builder.Property(r => r.OverrideStartsAt).HasColumnName("override_starts_at");
        builder.Property(r => r.OverrideEndsAt).HasColumnName("override_ends_at");

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
