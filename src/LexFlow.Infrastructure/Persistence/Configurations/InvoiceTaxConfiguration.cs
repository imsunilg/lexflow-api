using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class InvoiceTaxConfiguration : IEntityTypeConfiguration<InvoiceTax>
{
    public void Configure(EntityTypeBuilder<InvoiceTax> builder)
    {
        builder.ToTable("invoice_taxes", "fin");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(t => t.InvoiceId).HasColumnName("invoice_id").IsRequired();
        builder.Property(t => t.Name).HasColumnName("name").IsRequired();
        builder.Property(t => t.RatePct).HasColumnName("rate_pct").HasColumnType("numeric(6,3)").IsRequired();
        builder.Property(t => t.TaxableAmount).HasColumnName("taxable_amount").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(t => t.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").IsRequired();

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
