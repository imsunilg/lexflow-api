using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class WorkflowRuleConfiguration : IEntityTypeConfiguration<WorkflowRule>
{
    public void Configure(EntityTypeBuilder<WorkflowRule> builder)
    {
        builder.ToTable("workflow_rules", "ops");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id).HasColumnName("id");
        builder.Property(w => w.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(w => w.Name).HasColumnName("name").IsRequired();
        builder.Property(w => w.TriggerEvent).HasColumnName("trigger_event").IsRequired();
        builder.Property(w => w.ConditionsJson).HasColumnName("conditions").HasColumnType("jsonb").IsRequired();
        builder.Property(w => w.ActionsJson).HasColumnName("actions").HasColumnType("jsonb").IsRequired();
        builder.Property(w => w.Active).HasColumnName("active").IsRequired();
        builder.Property(w => w.RunOrder).HasColumnName("run_order").IsRequired();

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
