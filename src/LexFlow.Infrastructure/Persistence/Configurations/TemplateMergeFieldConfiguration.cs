using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class TemplateMergeFieldConfiguration : IEntityTypeConfiguration<TemplateMergeField>
{
    public void Configure(EntityTypeBuilder<TemplateMergeField> builder)
    {
        builder.ToTable("template_merge_fields", "dms");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id).HasColumnName("id");
        builder.Property(f => f.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(f => f.TemplateId).HasColumnName("template_id").IsRequired();
        builder.Property(f => f.FieldKey).HasColumnName("field_key").IsRequired();
        builder.Property(f => f.Label).HasColumnName("label");
        builder.Property(f => f.Required).HasColumnName("required").IsRequired();

        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(f => f.CreatedBy).HasColumnName("created_by");
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at");
        builder.Property(f => f.UpdatedBy).HasColumnName("updated_by");
        builder.Property(f => f.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(f => f.DeletedAt).HasColumnName("deleted_at");
        builder.Property(f => f.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(f => !f.IsDeleted);
    }
}
