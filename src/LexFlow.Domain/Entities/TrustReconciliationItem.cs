using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to fin.trust_reconciliation_items (lexflow-database Scripts/06_Fin/TrustReconciliationItems). One imported bank-statement line, auto- or manually matched to a fin.trust_ledger_entries row.</summary>
public sealed class TrustReconciliationItem : AuditableEntity
{
    private TrustReconciliationItem()
    {
    }

    public TrustReconciliationItem(Guid tenantId, Guid reconciliationId, Guid? trustAccountId, DateOnly bankLineDate, string? bankLineDescription, decimal bankLineAmount, Guid? matchedLedgerEntryId)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ReconciliationId = reconciliationId;
        TrustAccountId = trustAccountId;
        BankLineDate = bankLineDate;
        BankLineDescription = bankLineDescription;
        BankLineAmount = bankLineAmount;
        MatchedLedgerEntryId = matchedLedgerEntryId;
        IsException = matchedLedgerEntryId is null;
    }

    public Guid ReconciliationId { get; private set; }
    public Guid? TrustAccountId { get; private set; }
    public DateOnly BankLineDate { get; private set; }
    public string? BankLineDescription { get; private set; }
    public decimal BankLineAmount { get; private set; }
    public Guid? MatchedLedgerEntryId { get; private set; }
    public bool IsException { get; private set; }

    public void MatchTo(Guid ledgerEntryId, Guid trustAccountId)
    {
        MatchedLedgerEntryId = ledgerEntryId;
        TrustAccountId = trustAccountId;
        IsException = false;
    }
}
