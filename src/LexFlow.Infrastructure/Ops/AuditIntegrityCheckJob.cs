using System.Text.Json;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexFlow.Infrastructure.Ops;

/// <summary>
/// §8 Global Acceptance Criteria: G-AC1 ("Correctness of money: Σ(invoice totals) −
/// Σ(credit notes) − Σ(allocated payments) = total AR everywhere. Nightly integrity job
/// asserts this; violation pages on-call.") and AC-CC3 ("No court case can be in state
/// 'no future hearing &amp; not disposed &amp; not sine-die' (DB constraint + nightly
/// integrity job)"). Also sweeps for G-AC2 breaches retroactively: G-AC2 itself is a
/// same-transaction write-time invariant (a hearing/deadline's reminder rows are created
/// alongside it), so this job cannot enforce it going forward — it can only catch any
/// creation path that slipped through without reminders, which is exactly what a nightly
/// sweep is for.
///
/// §29 Alerting: "nightly integrity job failures (page)." No bespoke PagerDuty/paging
/// client exists anywhere in this codebase; every violation is written at Critical log
/// level (this environment's log-based alerting pipeline, §29, pages on Critical) and
/// persisted as an audit.audit_events row (ActorType "System") so it is visible in the
/// same audit trail admins already use, not a separate silent channel.
/// </summary>
public sealed class AuditIntegrityCheckJob(LexFlowDbContext db, ILogger<AuditIntegrityCheckJob> logger)
{
    private sealed record Violation(Guid TenantId, string Code, string Detail);

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var violations = new List<Violation>();
        violations.AddRange(await CheckMoneyReconciliationAsync(cancellationToken));
        violations.AddRange(await CheckReminderCoverageAsync(cancellationToken));
        violations.AddRange(await CheckCourtCaseInvariantAsync(cancellationToken));

        if (violations.Count == 0)
        {
            return;
        }

        foreach (var group in violations.GroupBy(v => v.TenantId))
        {
            logger.LogCritical(
                "Nightly integrity job found {Count} violation(s) for tenant {TenantId}: {Codes}",
                group.Count(), group.Key, string.Join(", ", group.Select(v => v.Code).Distinct()));

            await db.AuditEvents.AddAsync(new AuditEvent(
                group.Key,
                actorUserId: null,
                actorType: "System",
                action: "integrity.violation",
                entityType: "IntegrityCheck",
                entityId: null,
                before: null,
                after: JsonSerializer.Serialize(group.Select(v => new { v.Code, v.Detail })),
                ip: null,
                ua: null,
                traceId: null), cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>G-AC1: computed AR (GrandTotal − issued/applied credit notes − amount paid) must never go negative, and no single invoice may show more paid than its GrandTotal.</summary>
    private async Task<List<Violation>> CheckMoneyReconciliationAsync(CancellationToken cancellationToken)
    {
        var violations = new List<Violation>();

        var invoiceTotalsByTenant = await db.Invoices
            .Where(i => i.Status != "Draft" && i.Status != "Void")
            .GroupBy(i => i.TenantId)
            .Select(g => new { TenantId = g.Key, GrandTotal = g.Sum(i => i.GrandTotal), AmountPaid = g.Sum(i => i.AmountPaid) })
            .ToListAsync(cancellationToken);

        var creditTotalsByTenant = await db.CreditNotes
            .Where(c => c.Status == "Issued" || c.Status == "Applied")
            .GroupBy(c => c.TenantId)
            .Select(g => new { TenantId = g.Key, Amount = g.Sum(c => c.Amount) })
            .ToDictionaryAsync(g => g.TenantId, g => g.Amount, cancellationToken);

        foreach (var tenant in invoiceTotalsByTenant)
        {
            var credits = creditTotalsByTenant.GetValueOrDefault(tenant.TenantId);
            var ar = tenant.GrandTotal - credits - tenant.AmountPaid;

            if (ar < -0.01m)
            {
                violations.Add(new Violation(tenant.TenantId, "G-AC1_NEGATIVE_AR",
                    $"Computed AR is negative ({ar:0.00}): GrandTotal {tenant.GrandTotal:0.00} - Credits {credits:0.00} - AmountPaid {tenant.AmountPaid:0.00}."));
            }
        }

        var overpaidInvoices = await db.Invoices
            .Where(i => i.AmountPaid > i.GrandTotal + 0.01m)
            .Select(i => new { i.Id, i.TenantId, i.AmountPaid, i.GrandTotal })
            .ToListAsync(cancellationToken);

        violations.AddRange(overpaidInvoices.Select(inv => new Violation(inv.TenantId, "G-AC1_INVOICE_OVERPAID",
            $"Invoice {inv.Id} AmountPaid {inv.AmountPaid:0.00} exceeds GrandTotal {inv.GrandTotal:0.00}.")));

        return violations;
    }

    /// <summary>G-AC2 retroactive sweep: every future scheduled hearing and every unsatisfied important date must carry at least one event_reminders row.</summary>
    private async Task<List<Violation>> CheckReminderCoverageAsync(CancellationToken cancellationToken)
    {
        var violations = new List<Violation>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var reminderedHearingIds = (await db.EventReminders
            .Where(r => r.EventRefKind == "hearing")
            .Select(r => r.EventRefId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var unreminderdHearings = await db.Hearings
            .Where(h => h.Status == "Scheduled" && h.Date >= today)
            .Select(h => new { h.Id, h.TenantId })
            .ToListAsync(cancellationToken);

        violations.AddRange(unreminderdHearings
            .Where(h => !reminderedHearingIds.Contains(h.Id))
            .Select(h => new Violation(h.TenantId, "G-AC2_HEARING_MISSING_REMINDERS", $"Hearing {h.Id} has no event_reminders rows.")));

        var reminderedDateIds = (await db.EventReminders
            .Where(r => r.EventRefKind == "matter_important_date")
            .Select(r => r.EventRefId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var unsatisfiedDates = await db.MatterImportantDates
            .Where(d => d.SatisfiedAt == null)
            .Select(d => new { d.Id, d.TenantId })
            .ToListAsync(cancellationToken);

        violations.AddRange(unsatisfiedDates
            .Where(d => !reminderedDateIds.Contains(d.Id))
            .Select(d => new Violation(d.TenantId, "G-AC2_IMPORTANT_DATE_MISSING_REMINDERS", $"MatterImportantDate {d.Id} has no event_reminders rows.")));

        return violations;
    }

    /// <summary>AC-CC3: no court case may sit "Active" with no future scheduled hearing, not Disposed, not SineDie.</summary>
    private async Task<List<Violation>> CheckCourtCaseInvariantAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var activeCases = await db.CourtCases
            .Where(c => c.Status == "Active")
            .Select(c => new { c.Id, c.TenantId })
            .ToListAsync(cancellationToken);

        var caseIdsWithFutureHearing = (await db.Hearings
            .Where(h => h.Status == "Scheduled" && h.Date >= today)
            .Select(h => h.CaseId)
            .Distinct()
            .ToListAsync(cancellationToken))
            .ToHashSet();

        return activeCases
            .Where(c => !caseIdsWithFutureHearing.Contains(c.Id))
            .Select(c => new Violation(c.TenantId, "AC-CC3_NO_FUTURE_HEARING", $"CourtCase {c.Id} is Active with no future scheduled hearing, not Disposed, not SineDie."))
            .ToList();
    }
}
