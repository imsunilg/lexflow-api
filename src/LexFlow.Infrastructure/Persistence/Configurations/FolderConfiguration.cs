using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class FolderConfiguration : IEntityTypeConfiguration<Folder>
{
    public void Configure(EntityTypeBuilder<Folder> builder)
    {
        builder.ToTable("folders", "dms");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id).HasColumnName("id");
        builder.Property(f => f.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(f => f.ParentId).HasColumnName("parent_id");
        builder.Property(f => f.Name).HasColumnName("name").IsRequired();
        builder.Property(f => f.MatterId).HasColumnName("matter_id");
        builder.Property(f => f.Path).HasColumnName("path").HasColumnType("ltree");

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
