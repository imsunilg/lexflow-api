using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions", "core");
        builder.HasKey(rp => new { rp.RoleId, rp.PermissionId });

        builder.Property(rp => rp.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(rp => rp.RoleId).HasColumnName("role_id");
        builder.Property(rp => rp.PermissionId).HasColumnName("permission_id");

        builder.Property(rp => rp.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(rp => rp.CreatedBy).HasColumnName("created_by");
        builder.Property(rp => rp.UpdatedAt).HasColumnName("updated_at");
        builder.Property(rp => rp.UpdatedBy).HasColumnName("updated_by");
        builder.Property(rp => rp.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(rp => rp.DeletedAt).HasColumnName("deleted_at");
        builder.Property(rp => rp.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(rp => !rp.IsDeleted);
    }
}
