using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class TrustLedgerEntryConfiguration : IEntityTypeConfiguration<TrustLedgerEntry>
{
    public void Configure(EntityTypeBuilder<TrustLedgerEntry> builder)
    {
        builder.ToTable("trust_ledger_entries", "fin");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(t => t.TrustAccountId).HasColumnName("trust_account_id").IsRequired();
        // entry_no / running_balance are computed by the DB trigger (BEFORE INSERT) — see
        // TrustLedgerEntry's own doc comment. Nullable at the DB, so the CLR long default (0)
        // round-trips fine for an unsaved instance; EF just re-reads the DB-assigned value after
        // SaveChanges.
        builder.Property(t => t.EntryNo).HasColumnName("entry_no");
        builder.Property(t => t.Kind).HasColumnName("kind").IsRequired();
        builder.Property(t => t.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(t => t.RunningBalance).HasColumnName("running_balance").HasColumnType("numeric(18,2)");
        builder.Property(t => t.Purpose).HasColumnName("purpose");
        builder.Property(t => t.InvoiceId).HasColumnName("invoice_id");
        builder.Property(t => t.AuthorizationRef).HasColumnName("authorization_ref");
        builder.Property(t => t.ApprovedBy).HasColumnName("approved_by");
        builder.Property(t => t.SecondApproverId).HasColumnName("second_approver_id");
        builder.Property(t => t.ReversalOfId).HasColumnName("reversal_of_id");

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
