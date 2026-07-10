using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class CommTemplateConfiguration : IEntityTypeConfiguration<CommTemplate>
{
    public void Configure(EntityTypeBuilder<CommTemplate> builder)
    {
        builder.ToTable("comm_templates", "comm");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(c => c.Channel).HasColumnName("channel").IsRequired();
        builder.Property(c => c.Name).HasColumnName("name").IsRequired();
        builder.Property(c => c.Body).HasColumnName("body").IsRequired();
        builder.Property(c => c.VariablesJson).HasColumnName("variables").HasColumnType("jsonb").IsRequired();
        builder.Property(c => c.DltTemplateId).HasColumnName("dlt_template_id");
        builder.Property(c => c.WaHsmName).HasColumnName("wa_hsm_name");
        builder.Property(c => c.IsActive).HasColumnName("is_active").IsRequired();

        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");
        builder.Property(c => c.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(c => c.DeletedAt).HasColumnName("deleted_at");
        builder.Property(c => c.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
