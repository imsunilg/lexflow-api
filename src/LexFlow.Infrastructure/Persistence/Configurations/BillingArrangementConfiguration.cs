using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class BillingArrangementConfiguration : IEntityTypeConfiguration<BillingArrangement>
{
    public void Configure(EntityTypeBuilder<BillingArrangement> builder)
    {
        builder.ToTable("billing_arrangements", "fin");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id).HasColumnName("id");
        builder.Property(b => b.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(b => b.MatterId).HasColumnName("matter_id").IsRequired();
        builder.Property(b => b.ArrangementType).HasColumnName("arrangement_type").IsRequired();
        builder.Property(b => b.RateCardId).HasColumnName("rate_card_id");
        builder.Property(b => b.FixedAmount).HasColumnName("fixed_amount").HasColumnType("numeric(18,2)");
        builder.Property(b => b.MilestonesJson).HasColumnName("milestones").HasColumnType("jsonb").IsRequired();
        builder.Property(b => b.RetainerAmount).HasColumnName("retainer_amount").HasColumnType("numeric(18,2)");
        builder.Property(b => b.RetainerPeriod).HasColumnName("retainer_period");
        builder.Property(b => b.AutoInvoiceDay).HasColumnName("auto_invoice_day");
        builder.Property(b => b.ReplenishmentThreshold).HasColumnName("replenishment_threshold").HasColumnType("numeric(18,2)");
        builder.Property(b => b.ContingencyPct).HasColumnName("contingency_pct").HasColumnType("numeric(5,2)");
        builder.Property(b => b.IsActive).HasColumnName("is_active").IsRequired();

        builder.Property(b => b.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(b => b.CreatedBy).HasColumnName("created_by");
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at");
        builder.Property(b => b.UpdatedBy).HasColumnName("updated_by");
        builder.Property(b => b.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(b => b.DeletedAt).HasColumnName("deleted_at");
        builder.Property(b => b.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(b => !b.IsDeleted);
    }
}
