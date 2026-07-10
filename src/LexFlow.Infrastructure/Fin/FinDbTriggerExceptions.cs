using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Fin;

/// <summary>
/// Defense in depth (this module's own build brief): every fin.* DB trigger listed in
/// lexflow-database Scripts/06_Fin/*/004_Triggers.sql is also re-checked in the domain entity
/// itself (TimeEntry/Invoice throw InvalidOperationException before ever reaching SaveChanges),
/// so under EF InMemory (unit tests) or any application-layer-only path these translators never
/// actually fire. They exist for the real-Postgres path: a DbUpdateException whose inner message
/// carries the trigger's own RAISE EXCEPTION text (see each trigger's SQL) is translated to the
/// matching §17 DomainRuleException sub-code here, so no caller of these services is ever exposed
/// to a raw Postgres error message.
/// </summary>
public static class FinDbTriggerExceptions
{
    public static bool IsTimeEntryBilledError(DbUpdateException ex) => ex.InnerException?.Message.Contains("BR-5", StringComparison.OrdinalIgnoreCase) == true;

    public static bool IsInvoiceNotDraftError(DbUpdateException ex) => ex.InnerException?.Message.Contains("BR-4", StringComparison.OrdinalIgnoreCase) == true;

    public static bool IsInsufficientTrustBalanceError(DbUpdateException ex) => ex.InnerException?.Message.Contains("INSUFFICIENT_TRUST_BALANCE", StringComparison.OrdinalIgnoreCase) == true;

    public static bool IsTrustLedgerAppendOnlyError(DbUpdateException ex) => ex.InnerException?.Message.Contains("append-only", StringComparison.OrdinalIgnoreCase) == true;
}
