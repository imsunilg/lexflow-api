using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to fin.trust_ledger_entries (lexflow-database Scripts/06_Fin/TrustLedgerEntries).
/// BR-3/AC-B7: append-only — the DB trigger rejects any UPDATE/DELETE outright, and this entity
/// exposes no mutators at all (not even private ones beyond construction) so the application layer
/// has no path that could even attempt one. entry_no and running_balance are left unset by the
/// constructor (0/null in memory) — the BEFORE INSERT DB trigger computes both while holding a row
/// lock on the parent fin.trust_accounts row (AC-B4's concurrent-race guarantee); EF reads them back
/// after SaveChanges the same way it does for entry_no everywhere else this pattern is used.
/// </summary>
public sealed class TrustLedgerEntry : AuditableEntity
{
    private TrustLedgerEntry()
    {
    }

    public TrustLedgerEntry(Guid tenantId, Guid trustAccountId, string kind, decimal amount, string? purpose, Guid? invoiceId, string? authorizationRef, Guid? approvedBy, Guid? secondApproverId, Guid? reversalOfId)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        TrustAccountId = trustAccountId;
        Kind = kind;
        Amount = amount;
        Purpose = purpose;
        InvoiceId = invoiceId;
        AuthorizationRef = authorizationRef;
        ApprovedBy = approvedBy;
        SecondApproverId = secondApproverId;
        ReversalOfId = reversalOfId;
    }

    public Guid TrustAccountId { get; private set; }
    public long EntryNo { get; private set; }
    public string Kind { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public decimal RunningBalance { get; private set; }
    public string? Purpose { get; private set; }
    public Guid? InvoiceId { get; private set; }
    public string? AuthorizationRef { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public Guid? SecondApproverId { get; private set; }
    public Guid? ReversalOfId { get; private set; }
}
