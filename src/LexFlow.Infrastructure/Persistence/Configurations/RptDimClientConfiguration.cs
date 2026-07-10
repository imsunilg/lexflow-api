using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class RptDimClientConfiguration : IEntityTypeConfiguration<RptDimClient>
{
    public void Configure(EntityTypeBuilder<RptDimClient> builder)
    {
        builder.ToTable("rpt_dim_client", "rpt");
        builder.HasKey(c => c.ClientKey);

        builder.Property(c => c.ClientKey).HasColumnName("client_key");
        builder.Property(c => c.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(c => c.DisplayName).HasColumnName("display_name");
        builder.Property(c => c.ClientType).HasColumnName("client_type");
        builder.Property(c => c.BranchId).HasColumnName("branch_id");
        builder.Property(c => c.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").IsRequired();
    }
}
