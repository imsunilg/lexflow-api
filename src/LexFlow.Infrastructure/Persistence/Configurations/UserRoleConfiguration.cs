using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles", "core");
        builder.HasKey(ur => new { ur.UserId, ur.RoleId });

        builder.Property(ur => ur.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(ur => ur.UserId).HasColumnName("user_id");
        builder.Property(ur => ur.RoleId).HasColumnName("role_id");

        builder.Property(ur => ur.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(ur => ur.CreatedBy).HasColumnName("created_by");
        builder.Property(ur => ur.UpdatedAt).HasColumnName("updated_at");
        builder.Property(ur => ur.UpdatedBy).HasColumnName("updated_by");
        builder.Property(ur => ur.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(ur => ur.DeletedAt).HasColumnName("deleted_at");
        builder.Property(ur => ur.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(ur => !ur.IsDeleted);
    }
}
