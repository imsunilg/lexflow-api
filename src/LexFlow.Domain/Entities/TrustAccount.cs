using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to fin.trust_accounts (lexflow-database Scripts/06_Fin/TrustAccounts).
/// BR-3: "separate trust ledger per client... Balance ≥ 0 enforced at DB." <see cref="CurrentBalance"/>
/// is a denormalized cache that only the DB trigger on fin.trust_ledger_entries writes (via a
/// locked UPDATE) — this entity never mutates it directly; EF simply reads back whatever the
/// trigger wrote after SaveChanges.
/// </summary>
public sealed class TrustAccount : AuditableEntity
{
    private TrustAccount()
    {
    }

    public TrustAccount(Guid tenantId, Guid clientId, string? bankRef)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ClientId = clientId;
        BankRef = bankRef;
        CurrentBalance = 0;
    }

    public Guid ClientId { get; private set; }
    public string? BankRef { get; private set; }
    public decimal CurrentBalance { get; private set; }
}
