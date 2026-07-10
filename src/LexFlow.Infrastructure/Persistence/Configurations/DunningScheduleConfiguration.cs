using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class DunningScheduleConfiguration : IEntityTypeConfiguration<DunningSchedule>
{
    public void Configure(EntityTypeBuilder<DunningSchedule> builder)
    {
        builder.ToTable("dunning_schedules", "fin");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(d => d.Name).HasColumnName("name").IsRequired();
        builder.Property(d => d.StepsJson).HasColumnName("steps").HasColumnType("jsonb").IsRequired();
        builder.Property(d => d.IsActive).HasColumnName("is_active").IsRequired();

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
