using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class TaskTemplateItemConfiguration : IEntityTypeConfiguration<TaskTemplateItem>
{
    public void Configure(EntityTypeBuilder<TaskTemplateItem> builder)
    {
        builder.ToTable("task_template_items", "ops");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(i => i.TemplateId).HasColumnName("template_id").IsRequired();
        builder.Property(i => i.Title).HasColumnName("title").IsRequired();
        builder.Property(i => i.RelativeDueDays).HasColumnName("relative_due_days").IsRequired();
        builder.Property(i => i.Category).HasColumnName("category");
        builder.Property(i => i.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(i => i.IsMandatory).HasColumnName("is_mandatory").IsRequired();

        builder.Property(i => i.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(i => i.CreatedBy).HasColumnName("created_by");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");
        builder.Property(i => i.UpdatedBy).HasColumnName("updated_by");
        builder.Property(i => i.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(i => i.DeletedAt).HasColumnName("deleted_at");
        builder.Property(i => i.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}
