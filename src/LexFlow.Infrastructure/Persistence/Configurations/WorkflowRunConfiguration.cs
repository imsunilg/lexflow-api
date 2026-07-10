using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class WorkflowRunConfiguration : IEntityTypeConfiguration<WorkflowRun>
{
    public void Configure(EntityTypeBuilder<WorkflowRun> builder)
    {
        builder.ToTable("workflow_runs", "ops");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id).HasColumnName("id");
        builder.Property(w => w.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(w => w.RuleId).HasColumnName("rule_id").IsRequired();
        builder.Property(w => w.TriggerRef).HasColumnName("trigger_ref");
        builder.Property(w => w.Status).HasColumnName("status").IsRequired();
        builder.Property(w => w.ExecutedAt).HasColumnName("executed_at");
        builder.Property(w => w.ResultJson).HasColumnName("result").HasColumnType("jsonb").IsRequired();

        builder.Property(w => w.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(w => w.CreatedBy).HasColumnName("created_by");
        builder.Property(w => w.UpdatedAt).HasColumnName("updated_at");
        builder.Property(w => w.UpdatedBy).HasColumnName("updated_by");
        builder.Property(w => w.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(w => w.DeletedAt).HasColumnName("deleted_at");
        builder.Property(w => w.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(w => !w.IsDeleted);
    }
}
