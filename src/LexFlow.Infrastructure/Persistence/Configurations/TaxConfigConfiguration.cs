using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class TaxConfigConfiguration : IEntityTypeConfiguration<TaxConfig>
{
    public void Configure(EntityTypeBuilder<TaxConfig> builder)
    {
        builder.ToTable("tax_configs", "fin");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(t => t.BranchId).HasColumnName("branch_id");
        builder.Property(t => t.CountryCode).HasColumnName("country_code").IsRequired();
        builder.Property(t => t.TaxType).HasColumnName("tax_type").IsRequired();
        builder.Property(t => t.ComponentsJson).HasColumnName("components").HasColumnType("jsonb").IsRequired();
        builder.Property(t => t.IsActive).HasColumnName("is_active").IsRequired();

        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.CreatedBy).HasColumnName("created_by");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by");
        builder.Property(t => t.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(t => t.DeletedAt).HasColumnName("deleted_at");
        builder.Property(t => t.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
