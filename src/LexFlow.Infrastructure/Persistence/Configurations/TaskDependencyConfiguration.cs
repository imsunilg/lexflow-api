using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class TaskDependencyConfiguration : IEntityTypeConfiguration<TaskDependency>
{
    public void Configure(EntityTypeBuilder<TaskDependency> builder)
    {
        builder.ToTable("task_dependencies", "ops");
        builder.HasKey(d => new { d.TaskId, d.DependsOnTaskId });

        builder.Property(d => d.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(d => d.TaskId).HasColumnName("task_id");
        builder.Property(d => d.DependsOnTaskId).HasColumnName("depends_on_task_id");
        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(d => d.CreatedBy).HasColumnName("created_by");
    }
}
