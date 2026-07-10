using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class UserPermissionGrantConfiguration : IEntityTypeConfiguration<UserPermissionGrant>
{
    public void Configure(EntityTypeBuilder<UserPermissionGrant> builder)
    {
        builder.ToTable("user_permission_grants", "core");
        builder.HasKey(g => new { g.UserId, g.PermissionId });

        builder.Property(g => g.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(g => g.UserId).HasColumnName("user_id");
        builder.Property(g => g.PermissionId).HasColumnName("permission_id");
        builder.Property(g => g.GrantedBy).HasColumnName("granted_by");

        builder.Property(g => g.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(g => g.CreatedBy).HasColumnName("created_by");
        builder.Property(g => g.UpdatedAt).HasColumnName("updated_at");
        builder.Property(g => g.UpdatedBy).HasColumnName("updated_by");
        builder.Property(g => g.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(g => g.DeletedAt).HasColumnName("deleted_at");
        builder.Property(g => g.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(g => !g.IsDeleted);
    }
}
