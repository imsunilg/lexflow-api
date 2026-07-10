using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class GatewayConfigConfiguration : IEntityTypeConfiguration<GatewayConfig>
{
    public void Configure(EntityTypeBuilder<GatewayConfig> builder)
    {
        builder.ToTable("gateway_configs", "core");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id).HasColumnName("id");
        builder.Property(g => g.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(g => g.Provider).HasColumnName("provider").IsRequired();
        builder.Property(g => g.ConfigJson).HasColumnName("config_json").HasColumnType("jsonb").IsRequired();
        builder.Property(g => g.SecretKeyVaultRef).HasColumnName("secret_key_vault_ref");
        builder.Property(g => g.IsEnabled).HasColumnName("is_enabled").IsRequired();
        builder.Property(g => g.IsTestMode).HasColumnName("is_test_mode").IsRequired();

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
