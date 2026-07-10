using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Fin;

/// <summary>
/// Module 8: trust (client money) accounting. BR-3/AC-B4/AC-B7 are enforced first at the DB
/// (fin.trust_ledger_entries 004_Triggers.sql: a BEFORE INSERT trigger locks the parent
/// trust_accounts row FOR UPDATE, computes entry_no/running_balance, and raises
/// INSUFFICIENT_TRUST_BALANCE on overdraft; a second pair of triggers rejects any UPDATE/DELETE
/// outright) and again defensively here: any DbUpdateException carrying the trigger's own text is
/// translated to the matching DomainRuleException sub-code, so callers never see a raw Postgres
/// error (defense in depth, per this module's build brief).
/// </summary>
public sealed class TrustService(LexFlowDbContext db) : ITrustService
{
    public async Task<TrustAccountDto> GetOrCreateAccountAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default)
    {
        var account = await db.TrustAccounts.SingleOrDefaultAsync(a => a.TenantId == tenantId && a.ClientId == clientId, cancellationToken);
        if (account is not null)
        {
            return ToDto(account);
        }

        account = new TrustAccount(tenantId, clientId, null);
        await db.TrustAccounts.AddAsync(account, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(account);
    }

    public async Task<TrustLedgerEntryDto> DepositAsync(Guid tenantId, Guid actorId, Guid clientId, decimal amount, string? purpose, string? authorizationRef, CancellationToken cancellationToken = default)
    {
        var account = await GetOrCreateAccountEntityAsync(tenantId, clientId, cancellationToken);
        var entry = new TrustLedgerEntry(tenantId, account.Id, "Deposit", amount, purpose, null, authorizationRef, actorId, null, null);
        return await SaveEntryAsync(entry, cancellationToken);
    }

    public async Task<TrustLedgerEntryDto> DisburseAsync(Guid tenantId, Guid actorId, Guid clientId, decimal amount, string? purpose, Guid? invoiceId, string authorizationRef, Guid? secondApproverId, CancellationToken cancellationToken = default)
    {
        // Security Rules: "trust.disburse (dual-control option: second approver required — Enterprise)".
        var tenant = await db.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant?.PlanTier == "Enterprise" && (secondApproverId is null || secondApproverId == actorId))
        {
            throw new DomainRuleException("SECOND_APPROVER_REQUIRED", "Enterprise tier requires a second, different approver for trust disbursements (dual control).");
        }

        var account = await GetOrCreateAccountEntityAsync(tenantId, clientId, cancellationToken);

        // BR-3/AC-B4 application-level guard (defense in depth — "never trust the DB alone"): the
        // authoritative, race-proof check is the DB trigger's locked SELECT...FOR UPDATE (see this
        // class's own doc comment), but this recomputes the balance from the ledger history itself
        // (not the trigger-owned trust_accounts.current_balance cache, which this layer never
        // touches) so the same rule is caught here first under both Postgres and EF InMemory.
        var currentBalance = await ComputeCurrentBalanceAsync(account.Id, cancellationToken);
        if (amount > currentBalance)
        {
            throw new DomainRuleException("INSUFFICIENT_TRUST_BALANCE", $"Disbursement of {amount} exceeds trust account {account.Id}'s balance of {currentBalance} (BR-3).");
        }

        var entry = new TrustLedgerEntry(tenantId, account.Id, "Disbursement", amount, purpose, invoiceId, authorizationRef, actorId, secondApproverId, null);
        var dto = await SaveEntryAsync(entry, cancellationToken);

        if (invoiceId.HasValue)
        {
            var invoice = await db.Invoices.SingleOrDefaultAsync(i => i.TenantId == tenantId && i.Id == invoiceId, cancellationToken);
            invoice?.ApplyPayment(amount);
            await db.SaveChangesAsync(cancellationToken);
        }

        return dto;
    }

    public async Task<TrustLedgerEntryDto> ReverseAsync(Guid tenantId, Guid actorId, Guid ledgerEntryId, string reason, CancellationToken cancellationToken = default)
    {
        var original = await db.TrustLedgerEntries.SingleOrDefaultAsync(e => e.TenantId == tenantId && e.Id == ledgerEntryId, cancellationToken)
            ?? throw new NotFoundException(nameof(TrustLedgerEntry), ledgerEntryId);

        // Reversing a Deposit removes funds (same as a disbursement) — mirrors the DB trigger's
        // own v_removes_funds logic exactly.
        if (original.Kind == "Deposit")
        {
            var currentBalance = await ComputeCurrentBalanceAsync(original.TrustAccountId, cancellationToken);
            if (original.Amount > currentBalance)
            {
                throw new DomainRuleException("INSUFFICIENT_TRUST_BALANCE", $"Reversing deposit {ledgerEntryId} of {original.Amount} exceeds trust account {original.TrustAccountId}'s balance of {currentBalance} (BR-3).");
            }
        }

        var entry = new TrustLedgerEntry(tenantId, original.TrustAccountId, "Reversal", original.Amount, reason, original.InvoiceId, original.AuthorizationRef, actorId, null, original.Id);
        var dto = await SaveEntryAsync(entry, cancellationToken);

        // Edge case: "trust deposit cheque bounces ... invoice payment auto-unapplied".
        if (original.Kind == "Deposit" && original.InvoiceId.HasValue)
        {
            var invoice = await db.Invoices.SingleOrDefaultAsync(i => i.TenantId == tenantId && i.Id == original.InvoiceId, cancellationToken);
            invoice?.UnapplyPayment(original.Amount);
            await db.SaveChangesAsync(cancellationToken);
        }

        return dto;
    }

    public async Task<IReadOnlyList<TrustLedgerEntryDto>> GetLedgerAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default)
    {
        var account = await db.TrustAccounts.SingleOrDefaultAsync(a => a.TenantId == tenantId && a.ClientId == clientId, cancellationToken);
        if (account is null)
        {
            return [];
        }

        var entries = await db.TrustLedgerEntries.Where(e => e.TenantId == tenantId && e.TrustAccountId == account.Id).OrderBy(e => e.EntryNo).ToListAsync(cancellationToken);
        return entries.Select(ToDto).ToList();
    }

    public async Task<TrustReconciliationDto> ImportReconciliationAsync(Guid tenantId, DateOnly periodStart, DateOnly periodEnd, decimal bankStatementBalance, IReadOnlyList<BankStatementLineInput> lines, string? importedCsvBlobPath, CancellationToken cancellationToken = default)
    {
        // Recomputed from the ledger itself, not the trigger-owned trust_accounts.current_balance
        // cache — same rationale as ComputeCurrentBalanceAsync (this class's own doc comment).
        var accountIds = await db.TrustAccounts.Where(a => a.TenantId == tenantId).Select(a => a.Id).ToListAsync(cancellationToken);
        decimal ledgerBalance = 0;
        foreach (var accountId in accountIds)
        {
            ledgerBalance += await ComputeCurrentBalanceAsync(accountId, cancellationToken);
        }
        var reconciliation = new TrustReconciliation(tenantId, periodStart, periodEnd, bankStatementBalance, ledgerBalance, importedCsvBlobPath);
        await db.TrustReconciliations.AddAsync(reconciliation, cancellationToken);

        // AC: reconciliation workspace "import -> auto-match -> exceptions list". Auto-match by
        // exact amount + same-day ledger entry (documented simplification of a real bank-line
        // matcher, which would also weigh description/reference text).
        var allEntries = await db.TrustLedgerEntries.Where(e => e.TenantId == tenantId).ToListAsync(cancellationToken);

        foreach (var line in lines)
        {
            var match = allEntries.FirstOrDefault(e => e.Amount == Math.Abs(line.Amount) && DateOnly.FromDateTime(e.CreatedAt.UtcDateTime) == line.Date);
            var item = new TrustReconciliationItem(tenantId, reconciliation.Id, match?.TrustAccountId, line.Date, line.Description, line.Amount, match?.Id);
            await db.TrustReconciliationItems.AddAsync(item, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return await BuildDtoAsync(reconciliation, cancellationToken);
    }

    public async Task<TrustReconciliationDto> SignOffReconciliationAsync(Guid tenantId, Guid actorId, Guid reconciliationId, string? notes, CancellationToken cancellationToken = default)
    {
        var reconciliation = await db.TrustReconciliations.SingleOrDefaultAsync(r => r.TenantId == tenantId && r.Id == reconciliationId, cancellationToken)
            ?? throw new NotFoundException(nameof(TrustReconciliation), reconciliationId);

        reconciliation.SignOff(actorId, notes);
        await db.SaveChangesAsync(cancellationToken);
        return await BuildDtoAsync(reconciliation, cancellationToken);
    }

    public async Task<IReadOnlyList<TrustReconciliationItemDto>> GetExceptionsAsync(Guid tenantId, Guid reconciliationId, CancellationToken cancellationToken = default)
    {
        var items = await db.TrustReconciliationItems
            .Where(i => i.TenantId == tenantId && i.ReconciliationId == reconciliationId && i.IsException)
            .ToListAsync(cancellationToken);
        return items.Select(i => new TrustReconciliationItemDto(i.Id, i.BankLineDate, i.BankLineDescription, i.BankLineAmount, i.IsException)).ToList();
    }

    /// <summary>Recomputes the balance from the append-only ledger itself (never from the trigger-owned trust_accounts.current_balance cache) — see DisburseAsync/ReverseAsync's own comments for why.</summary>
    private async Task<decimal> ComputeCurrentBalanceAsync(Guid trustAccountId, CancellationToken cancellationToken)
    {
        var entries = await db.TrustLedgerEntries.Where(e => e.TrustAccountId == trustAccountId).ToListAsync(cancellationToken);
        var byId = entries.ToDictionary(e => e.Id);

        decimal balance = 0;
        foreach (var entry in entries)
        {
            var removesFunds = entry.Kind switch
            {
                "Disbursement" => true,
                "Reversal" when entry.ReversalOfId.HasValue && byId.TryGetValue(entry.ReversalOfId.Value, out var reversed) => reversed.Kind == "Deposit",
                _ => false,
            };

            balance += removesFunds ? -entry.Amount : entry.Amount;
        }

        return balance;
    }

    private async Task<TrustAccount> GetOrCreateAccountEntityAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken)
    {
        var account = await db.TrustAccounts.SingleOrDefaultAsync(a => a.TenantId == tenantId && a.ClientId == clientId, cancellationToken);
        if (account is not null)
        {
            return account;
        }

        account = new TrustAccount(tenantId, clientId, null);
        await db.TrustAccounts.AddAsync(account, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return account;
    }

    private async Task<TrustLedgerEntryDto> SaveEntryAsync(TrustLedgerEntry entry, CancellationToken cancellationToken)
    {
        await db.TrustLedgerEntries.AddAsync(entry, cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (FinDbTriggerExceptions.IsInsufficientTrustBalanceError(ex))
        {
            throw new DomainRuleException("INSUFFICIENT_TRUST_BALANCE", $"This {entry.Kind.ToLowerInvariant()} of {entry.Amount} would take the trust account below zero (BR-3).");
        }

        // Under EF InMemory the DB trigger never runs (no Postgres), so entry_no/running_balance
        // stay at their in-memory defaults (0). Unit tests targeting this path assert on the
        // *domain rule* (insufficient-balance rejection), not on the trigger-computed running
        // balance itself — the real running balance is a documented Postgres-only guarantee, the
        // same InMemory-vs-Postgres gap already noted for several generated/trigger-computed
        // columns elsewhere in this codebase (client.DisplayName, ChatMessage.Seq).
        return ToDto(entry);
    }

    private async Task<TrustReconciliationDto> BuildDtoAsync(TrustReconciliation reconciliation, CancellationToken cancellationToken)
    {
        var exceptionCount = await db.TrustReconciliationItems.CountAsync(i => i.ReconciliationId == reconciliation.Id && i.IsException, cancellationToken);
        return new TrustReconciliationDto(reconciliation.Id, reconciliation.PeriodStart, reconciliation.PeriodEnd, reconciliation.BankStatementBalance, reconciliation.LedgerBalance, reconciliation.Status, reconciliation.IsBalanced, exceptionCount);
    }

    private static TrustAccountDto ToDto(TrustAccount a) => new(a.Id, a.ClientId, a.BankRef, a.CurrentBalance);

    private static TrustLedgerEntryDto ToDto(TrustLedgerEntry e) => new(e.Id, e.TrustAccountId, e.EntryNo, e.Kind, e.Amount, e.RunningBalance, e.Purpose, e.InvoiceId, e.CreatedAt);
}
