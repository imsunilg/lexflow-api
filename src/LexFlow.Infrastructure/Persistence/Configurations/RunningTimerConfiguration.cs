using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class RunningTimerConfiguration : IEntityTypeConfiguration<RunningTimer>
{
    public void Configure(EntityTypeBuilder<RunningTimer> builder)
    {
        builder.ToTable("running_timers", "fin");
        builder.HasKey(t => t.UserId);

        builder.Property(t => t.UserId).HasColumnName("user_id");
        builder.Property(t => t.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(t => t.MatterId).HasColumnName("matter_id");
        builder.Property(t => t.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(t => t.PausedMs).HasColumnName("paused_ms").IsRequired();
        builder.Property(t => t.ContextJson).HasColumnName("context").HasColumnType("jsonb").IsRequired();

        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.CreatedBy).HasColumnName("created_by");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by");
    }
}
