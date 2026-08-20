using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class PortalLoginHistoryConfiguration : IEntityTypeConfiguration<PortalLoginHistory>
{
    public void Configure(EntityTypeBuilder<PortalLoginHistory> builder)
    {
        builder.ToTable("portal_login_history", "portal");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id).HasColumnName("id");
        builder.Property(h => h.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(h => h.ClientPortalUserId).HasColumnName("client_portal_user_id");
        builder.Property(h => h.At).HasColumnName("at").IsRequired();
        builder.Property(h => h.Ip)
            .HasColumnName("ip")
            .HasColumnType("inet")
            .HasConversion(
                v => v == null ? null : System.Net.IPAddress.Parse(v),
                v => v == null ? null : v.ToString());
        builder.Property(h => h.Ua).HasColumnName("ua");
        builder.Property(h => h.Result).HasColumnName("result").IsRequired();
        builder.Property(h => h.FailureReason).HasColumnName("failure_reason");

        builder.Property(h => h.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(h => h.CreatedBy).HasColumnName("created_by");
        builder.Property(h => h.UpdatedAt).HasColumnName("updated_at");
        builder.Property(h => h.UpdatedBy).HasColumnName("updated_by");
        builder.Property(h => h.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(h => h.DeletedAt).HasColumnName("deleted_at");
        builder.Property(h => h.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(h => !h.IsDeleted);
    }
}
