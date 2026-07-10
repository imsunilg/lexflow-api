using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class TrustReconciliationItemConfiguration : IEntityTypeConfiguration<TrustReconciliationItem>
{
    public void Configure(EntityTypeBuilder<TrustReconciliationItem> builder)
    {
        builder.ToTable("trust_reconciliation_items", "fin");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(i => i.ReconciliationId).HasColumnName("reconciliation_id").IsRequired();
        builder.Property(i => i.TrustAccountId).HasColumnName("trust_account_id");
        builder.Property(i => i.BankLineDate).HasColumnName("bank_line_date").IsRequired();
        builder.Property(i => i.BankLineDescription).HasColumnName("bank_line_description");
        builder.Property(i => i.BankLineAmount).HasColumnName("bank_line_amount").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(i => i.MatchedLedgerEntryId).HasColumnName("matched_ledger_entry_id");
        builder.Property(i => i.IsException).HasColumnName("is_exception").IsRequired();

        builder.Property(i => i.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(i => i.CreatedBy).HasColumnName("created_by");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");
        builder.Property(i => i.UpdatedBy).HasColumnName("updated_by");
        builder.Property(i => i.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(i => i.DeletedAt).HasColumnName("deleted_at");
        builder.Property(i => i.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}
