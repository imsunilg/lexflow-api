using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class AiTenantQuotaConfiguration : IEntityTypeConfiguration<AiTenantQuota>
{
    public void Configure(EntityTypeBuilder<AiTenantQuota> builder)
    {
        builder.ToTable("ai_tenant_quotas", "ai");
        builder.HasKey(q => q.TenantId);

        builder.Property(q => q.TenantId).HasColumnName("tenant_id");
        builder.Property(q => q.MonthlyCreditLimit).HasColumnName("monthly_credit_limit").IsRequired();
        builder.Property(q => q.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(q => q.UpdatedAt).HasColumnName("updated_at");
    }
}
