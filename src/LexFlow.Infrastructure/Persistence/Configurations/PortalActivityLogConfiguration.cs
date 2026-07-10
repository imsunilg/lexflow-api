using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class PortalActivityLogConfiguration : IEntityTypeConfiguration<PortalActivityLog>
{
    public void Configure(EntityTypeBuilder<PortalActivityLog> builder)
    {
        builder.ToTable("portal_activity_log", "portal");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(l => l.ClientPortalUserId).HasColumnName("client_portal_user_id").IsRequired();
        builder.Property(l => l.Action).HasColumnName("action").IsRequired();
        builder.Property(l => l.EntityType).HasColumnName("entity_type").IsRequired();
        builder.Property(l => l.EntityId).HasColumnName("entity_id");
        builder.Property(l => l.Ip).HasColumnName("ip").HasColumnType("inet");
        builder.Property(l => l.Ua).HasColumnName("ua");
        builder.Property(l => l.At).HasColumnName("at").IsRequired();
    }
}
