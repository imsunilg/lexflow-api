using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class TrustReconciliationConfiguration : IEntityTypeConfiguration<TrustReconciliation>
{
    public void Configure(EntityTypeBuilder<TrustReconciliation> builder)
    {
        builder.ToTable("trust_reconciliations", "fin");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(r => r.PeriodStart).HasColumnName("period_start").IsRequired();
        builder.Property(r => r.PeriodEnd).HasColumnName("period_end").IsRequired();
        builder.Property(r => r.BankStatementBalance).HasColumnName("bank_statement_balance").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(r => r.LedgerBalance).HasColumnName("ledger_balance").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(r => r.Status).HasColumnName("status").IsRequired();
        builder.Property(r => r.ImportedCsvBlobPath).HasColumnName("imported_csv_blob_path");
        builder.Property(r => r.Notes).HasColumnName("notes");
        builder.Property(r => r.SignedOffBy).HasColumnName("signed_off_by");
        builder.Property(r => r.SignedOffAt).HasColumnName("signed_off_at");

        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by");
        builder.Property(r => r.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(r => r.DeletedAt).HasColumnName("deleted_at");
        builder.Property(r => r.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
