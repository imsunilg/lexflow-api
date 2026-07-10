namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 8: trust (client money) accounting. BR-3 (no negative balance, append-only ledger) is
/// enforced first at the DB (fin.trust_ledger_entries 004_Triggers.sql, which locks the parent
/// trust_accounts row FOR UPDATE so concurrent postings serialize — AC-B4) and again defensively
/// here: DbUpdateException carrying the trigger's INSUFFICIENT_TRUST_BALANCE text is translated to
/// DomainRuleException("INSUFFICIENT_TRUST_BALANCE", ...) so callers never see a raw Postgres error.
/// </summary>
public interface ITrustService
{
    Task<TrustAccountDto> GetOrCreateAccountAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default);

    Task<TrustLedgerEntryDto> DepositAsync(Guid tenantId, Guid actorId, Guid clientId, decimal amount, string? purpose, string? authorizationRef, CancellationToken cancellationToken = default);

    /// <summary>Security Rules: "trust.disburse (dual-control option: second approver required — Enterprise)." Throws DomainRuleException("SECOND_APPROVER_REQUIRED", ...) when the tenant's plan_tier is Enterprise and secondApproverId is missing or equals actorId.</summary>
    Task<TrustLedgerEntryDto> DisburseAsync(Guid tenantId, Guid actorId, Guid clientId, decimal amount, string? purpose, Guid? invoiceId, string authorizationRef, Guid? secondApproverId, CancellationToken cancellationToken = default);

    /// <summary>Edge case: "trust deposit cheque bounces (reversing entry + alert + invoice payment auto-unapplied)." A Reversal of a Deposit removes funds the same as a Disbursement (see the DB trigger's own v_removes_funds logic).</summary>
    Task<TrustLedgerEntryDto> ReverseAsync(Guid tenantId, Guid actorId, Guid ledgerEntryId, string reason, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TrustLedgerEntryDto>> GetLedgerAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default);

    Task<TrustReconciliationDto> ImportReconciliationAsync(Guid tenantId, DateOnly periodStart, DateOnly periodEnd, decimal bankStatementBalance, IReadOnlyList<BankStatementLineInput> lines, string? importedCsvBlobPath, CancellationToken cancellationToken = default);

    Task<TrustReconciliationDto> SignOffReconciliationAsync(Guid tenantId, Guid actorId, Guid reconciliationId, string? notes, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TrustReconciliationItemDto>> GetExceptionsAsync(Guid tenantId, Guid reconciliationId, CancellationToken cancellationToken = default);
}

public sealed record BankStatementLineInput(DateOnly Date, string? Description, decimal Amount);

public sealed record TrustAccountDto(Guid Id, Guid ClientId, string? BankRef, decimal CurrentBalance);

public sealed record TrustLedgerEntryDto(Guid Id, Guid TrustAccountId, long EntryNo, string Kind, decimal Amount, decimal RunningBalance, string? Purpose, Guid? InvoiceId, DateTimeOffset CreatedAt);

public sealed record TrustReconciliationDto(Guid Id, DateOnly PeriodStart, DateOnly PeriodEnd, decimal BankStatementBalance, decimal LedgerBalance, string Status, bool IsBalanced, int ExceptionCount);

public sealed record TrustReconciliationItemDto(Guid Id, DateOnly BankLineDate, string? BankLineDescription, decimal BankLineAmount, bool IsException);
