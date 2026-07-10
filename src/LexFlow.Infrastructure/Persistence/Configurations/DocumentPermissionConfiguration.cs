using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class DocumentPermissionConfiguration : IEntityTypeConfiguration<DocumentPermission>
{
    public void Configure(EntityTypeBuilder<DocumentPermission> builder)
    {
        builder.ToTable("document_permissions", "dms");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(p => p.DocumentId).HasColumnName("document_id").IsRequired();
        builder.Property(p => p.PrincipalType).HasColumnName("principal_type").IsRequired();
        builder.Property(p => p.PrincipalId).HasColumnName("principal_id").IsRequired();
        builder.Property(p => p.Access).HasColumnName("access").IsRequired();

        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");
        builder.Property(p => p.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(p => p.DeletedAt).HasColumnName("deleted_at");
        builder.Property(p => p.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
