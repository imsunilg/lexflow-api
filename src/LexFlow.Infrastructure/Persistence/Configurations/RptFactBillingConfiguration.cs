using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class RptFactBillingConfiguration : IEntityTypeConfiguration<RptFactBilling>
{
    public void Configure(EntityTypeBuilder<RptFactBilling> builder)
    {
        builder.ToTable("rpt_fact_billing", "rpt");
        builder.HasKey(f => f.FactBillingKey);

        builder.Property(f => f.FactBillingKey).HasColumnName("fact_billing_key");
        builder.Property(f => f.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(f => f.InvoiceId).HasColumnName("invoice_id").IsRequired();
        builder.Property(f => f.DateKey).HasColumnName("date_key");
        builder.Property(f => f.LawyerKey).HasColumnName("lawyer_key");
        builder.Property(f => f.ClientKey).HasColumnName("client_key");
        builder.Property(f => f.PracticeAreaKey).HasColumnName("practice_area_key");
        builder.Property(f => f.BranchId).HasColumnName("branch_id");
        builder.Property(f => f.BilledAmount).HasColumnName("billed_amount").IsRequired();
        builder.Property(f => f.CollectedAmount).HasColumnName("collected_amount").IsRequired();
        builder.Property(f => f.WriteOffAmount).HasColumnName("write_off_amount").IsRequired();
        builder.Property(f => f.OutstandingAmount).HasColumnName("outstanding_amount").IsRequired();
        builder.Property(f => f.TaxAmount).HasColumnName("tax_amount").IsRequired();
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(f => new { f.TenantId, f.InvoiceId }).IsUnique();
    }
}
