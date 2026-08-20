using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class PortalSessionConfiguration : IEntityTypeConfiguration<PortalSession>
{
    public void Configure(EntityTypeBuilder<PortalSession> builder)
    {
        builder.ToTable("portal_sessions", "portal");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(s => s.ClientPortalUserId).HasColumnName("client_portal_user_id").IsRequired();
        builder.Property(s => s.RefreshHash).HasColumnName("refresh_hash").IsRequired();
        builder.Property(s => s.FamilyId).HasColumnName("family_id").IsRequired();
        builder.Property(s => s.Ua).HasColumnName("ua");
        builder.Property(s => s.Ip)
            .HasColumnName("ip")
            .HasColumnType("inet")
            .HasConversion(
                v => v == null ? null : System.Net.IPAddress.Parse(v),
                v => v == null ? null : v.ToString());
        builder.Property(s => s.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(s => s.RevokedAt).HasColumnName("revoked_at");

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
