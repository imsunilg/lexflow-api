using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class KbActSectionConfiguration : IEntityTypeConfiguration<KbActSection>
{
    public void Configure(EntityTypeBuilder<KbActSection> builder)
    {
        builder.ToTable("kb_act_sections", "kb");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(s => s.ActId).HasColumnName("act_id").IsRequired();
        builder.Property(s => s.ParentId).HasColumnName("parent_id");
        builder.Property(s => s.Number).HasColumnName("number").IsRequired();
        builder.Property(s => s.Title).HasColumnName("title");
        builder.Property(s => s.Body).HasColumnName("body");
        builder.Property(s => s.EffectiveFrom).HasColumnName("effective_from");
        builder.Property(s => s.EffectiveTo).HasColumnName("effective_to");
        builder.Property(s => s.Path).HasColumnName("path").HasColumnType("ltree");

        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by");
        builder.Property(s => s.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(s => s.DeletedAt).HasColumnName("deleted_at");
        builder.Property(s => s.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
