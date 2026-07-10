using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class ClientPortalUserConfiguration : IEntityTypeConfiguration<ClientPortalUser>
{
    public void Configure(EntityTypeBuilder<ClientPortalUser> builder)
    {
        builder.ToTable("client_portal_users", "crm");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(u => u.ClientId).HasColumnName("client_id").IsRequired();
        builder.Property(u => u.Email).HasColumnName("email").IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash");
        builder.Property(u => u.Name).HasColumnName("name");
        builder.Property(u => u.Status).HasColumnName("status").IsRequired();
        builder.Property(u => u.TwoFaEnabled).HasColumnName("two_fa_enabled").IsRequired();
        builder.Property(u => u.VisibleMatterIds).HasColumnName("visible_matter_ids");

        builder.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(u => u.CreatedBy).HasColumnName("created_by");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");
        builder.Property(u => u.UpdatedBy).HasColumnName("updated_by");
        builder.Property(u => u.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(u => u.DeletedAt).HasColumnName("deleted_at");
        builder.Property(u => u.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}
