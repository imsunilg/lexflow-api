using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to fin.trust_reconciliations (lexflow-database Scripts/06_Fin/TrustReconciliations). Module 8: monthly three-way reconciliation with sign-off.</summary>
public sealed class TrustReconciliation : AuditableEntity
{
    private TrustReconciliation()
    {
    }

    public TrustReconciliation(Guid tenantId, DateOnly periodStart, DateOnly periodEnd, decimal bankStatementBalance, decimal ledgerBalance, string? importedCsvBlobPath)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        BankStatementBalance = bankStatementBalance;
        LedgerBalance = ledgerBalance;
        ImportedCsvBlobPath = importedCsvBlobPath;
        Status = "Draft";
    }

    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public decimal BankStatementBalance { get; private set; }
    public decimal LedgerBalance { get; private set; }
    public string Status { get; private set; } = "Draft";
    public string? ImportedCsvBlobPath { get; private set; }
    public string? Notes { get; private set; }
    public Guid? SignedOffBy { get; private set; }
    public DateTimeOffset? SignedOffAt { get; private set; }

    public bool IsBalanced => BankStatementBalance == LedgerBalance;

    public void SignOff(Guid signedOffBy, string? notes)
    {
        if (!IsBalanced)
        {
            throw new InvalidOperationException("A trust reconciliation with unresolved exceptions (bank statement balance != ledger balance) cannot be signed off.");
        }

        Status = "SignedOff";
        SignedOffBy = signedOffBy;
        SignedOffAt = DateTimeOffset.UtcNow;
        Notes = notes;
    }
}
