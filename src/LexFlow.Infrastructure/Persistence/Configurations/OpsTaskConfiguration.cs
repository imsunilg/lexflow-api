using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class OpsTaskConfiguration : IEntityTypeConfiguration<OpsTask>
{
    public void Configure(EntityTypeBuilder<OpsTask> builder)
    {
        builder.ToTable("tasks", "ops");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(t => t.Number).HasColumnName("number");
        builder.Property(t => t.Title).HasColumnName("title").IsRequired();
        builder.Property(t => t.Description).HasColumnName("description");
        builder.Property(t => t.MatterId).HasColumnName("matter_id");
        builder.Property(t => t.ClientId).HasColumnName("client_id");
        builder.Property(t => t.OwnerId).HasColumnName("owner_id");
        builder.Property(t => t.DueAt).HasColumnName("due_at");
        builder.Property(t => t.Priority).HasColumnName("priority").IsRequired();
        builder.Property(t => t.Category).HasColumnName("category");
        builder.Property(t => t.Status).HasColumnName("status").IsRequired();
        builder.Property(t => t.ProgressPct).HasColumnName("progress_pct").IsRequired();
        builder.Property(t => t.RecurrenceId).HasColumnName("recurrence_id");
        builder.Property(t => t.TemplateKey).HasColumnName("template_key");

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
