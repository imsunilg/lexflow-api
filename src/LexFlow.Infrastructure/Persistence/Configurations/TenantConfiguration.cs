using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants", "core");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.Name).HasColumnName("name").IsRequired();
        builder.Property(t => t.Slug).HasColumnName("slug").IsRequired();
        builder.Property(t => t.Status).HasColumnName("status").IsRequired();
        builder.Property(t => t.PlanTier).HasColumnName("plan_tier").IsRequired();
        builder.Property(t => t.Region).HasColumnName("region").IsRequired();
        builder.Property(t => t.DefaultLocale).HasColumnName("default_locale").IsRequired();
        builder.Property(t => t.DefaultCurrency).HasColumnName("default_currency").IsRequired();
        builder.Property(t => t.FiscalYearStartMonth).HasColumnName("fiscal_year_start_month").IsRequired();
        builder.Property(t => t.Timezone).HasColumnName("timezone").IsRequired();

        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(t => t.DeletedAt).HasColumnName("deleted_at");

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
