using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Reporting;

/// <summary>
/// Module 13: the 12 standard reports. The four "heavy aggregate" reports the PRD Database
/// section names explicitly (Revenue, Cases/Matters, Lawyer Performance, Practice Area) are
/// parameterized queries against the rpt.* star schema, per that section's own wording. The
/// remaining eight are current-state/list-style reports (Clients, Expenses, Billing, Payments,
/// Tasks, Hearings, Lead Conversion, Trust) that read their owning OLTP schema directly — they
/// don't need a nightly/hourly-stale denormalized copy, and reading the OLTP tables keeps their
/// totals trivially reconcilable against the corresponding module's own list view (AC-R1). Every
/// report applies <see cref="ReportScopeMatcher"/> before anything is aggregated.
/// </summary>
public sealed class StandardReportService(LexFlowDbContext db) : IStandardReportService
{
    private static readonly IReadOnlyList<ReportCatalogItem> Catalog =
    [
        new("revenue", "Revenue", "Finance", true),
        new("clients", "Clients", "CRM", false),
        new("matters", "Cases/Matters", "Legal", false),
        new("lawyer-performance", "Lawyer Performance", "Operations", false),
        new("expenses", "Expenses", "Finance", true),
        new("billing", "Billing", "Finance", true),
        new("payments", "Payments", "Finance", true),
        new("tasks", "Tasks", "Operations", false),
        new("hearings", "Hearings", "Legal", false),
        new("lead-conversion", "Lead Conversion", "CRM", false),
        new("practice-area", "Practice Area", "Operations", false),
        new("trust", "Trust", "Finance", true),
    ];

    public IReadOnlyList<ReportCatalogItem> GetCatalog() => Catalog;

    public async Task<ReportResult> RunAsync(Guid tenantId, string reportKey, ReportRunParams reportParams, ReportScope scope, CancellationToken cancellationToken = default)
    {
        ValidateDateRange(reportParams);

        return reportKey.ToLowerInvariant() switch
        {
            "revenue" => await RunRevenueAsync(tenantId, reportParams, scope, cancellationToken),
            "clients" => await RunClientsAsync(tenantId, reportParams, scope, cancellationToken),
            "matters" => await RunMattersAsync(tenantId, reportParams, scope, cancellationToken),
            "lawyer-performance" => await RunLawyerPerformanceAsync(tenantId, reportParams, scope, cancellationToken),
            "expenses" => await RunExpensesAsync(tenantId, reportParams, scope, cancellationToken),
            "billing" => await RunBillingAsync(tenantId, reportParams, scope, cancellationToken),
            "payments" => await RunPaymentsAsync(tenantId, reportParams, scope, cancellationToken),
            "tasks" => await RunTasksAsync(tenantId, reportParams, scope, cancellationToken),
            "hearings" => await RunHearingsAsync(tenantId, reportParams, scope, cancellationToken),
            "lead-conversion" => await RunLeadConversionAsync(tenantId, reportParams, scope, cancellationToken),
            "practice-area" => await RunPracticeAreaAsync(tenantId, reportParams, scope, cancellationToken),
            "trust" => await RunTrustAsync(tenantId, scope, cancellationToken),
            _ => throw new NotFoundException("ReportDefinition", reportKey),
        };
    }

    private static void ValidateDateRange(ReportRunParams reportParams)
    {
        if (reportParams.DateFrom.HasValue && reportParams.DateTo.HasValue
            && reportParams.DateTo.Value.ToDateTime(TimeOnly.MinValue) - reportParams.DateFrom.Value.ToDateTime(TimeOnly.MinValue) > TimeSpan.FromDays(3 * 366))
        {
            throw new DomainRuleException("DATE_RANGE_TOO_LARGE", "Module 13 Validation: date range must be 3 years or less per run.");
        }
    }

    // --- Revenue: rpt_fact_billing, grouped by calendar month (YYYY-MM). ---
    private async Task<ReportResult> RunRevenueAsync(Guid tenantId, ReportRunParams p, ReportScope scope, CancellationToken ct)
    {
        var facts = await db.RptFactBillings.Where(f => f.TenantId == tenantId).ToListAsync(ct);
        var filtered = facts
            .Where(f => ReportScopeMatcher.Matches(scope, f.LawyerKey, f.BranchId))
            .Where(f => MatchesDateKey(f.DateKey, p))
            .Where(f => p.PracticeAreaId is null || f.PracticeAreaKey == p.PracticeAreaId)
            .Where(f => p.LawyerId is null || f.LawyerKey == p.LawyerId)
            .Where(f => p.ClientId is null || f.ClientKey == p.ClientId)
            .Where(f => p.BranchId is null || f.BranchId == p.BranchId)
            .ToList();

        var rows = filtered
            .GroupBy(f => f.DateKey.HasValue ? f.DateKey.Value / 100 : 0)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var billed = g.Sum(f => f.BilledAmount);
                var collected = g.Sum(f => f.CollectedAmount);
                var writeOff = g.Sum(f => f.WriteOffAmount);
                var outstanding = g.Sum(f => f.OutstandingAmount);
                var realization = billed == 0 ? 0 : Math.Round(collected / billed * 100, 2);
                return (IReadOnlyList<object?>)new object?[] { FormatPeriod(g.Key), billed, collected, writeOff, outstanding, realization };
            })
            .ToList();

        return new ReportResult(["Period", "BilledAmount", "CollectedAmount", "WriteOffAmount", "OutstandingAmount", "RealizationRatePct"], rows);
    }

    // --- Clients: crm.clients (OLTP), revenue sourced from rpt_fact_billing by client. ---
    private async Task<ReportResult> RunClientsAsync(Guid tenantId, ReportRunParams p, ReportScope scope, CancellationToken ct)
    {
        var clients = await db.Clients.Where(c => c.TenantId == tenantId).ToListAsync(ct);
        var revenueByClient = await db.RptFactBillings.Where(f => f.TenantId == tenantId && f.ClientKey != null)
            .GroupBy(f => f.ClientKey!.Value)
            .Select(g => new { ClientKey = g.Key, Revenue = g.Sum(f => f.BilledAmount) })
            .ToDictionaryAsync(x => x.ClientKey, x => x.Revenue, ct);

        var rows = clients
            .Where(c => ReportScopeMatcher.Matches(scope, c.OwnerId, c.BranchId))
            .Where(c => p.BranchId is null || c.BranchId == p.BranchId)
            .OrderByDescending(c => revenueByClient.GetValueOrDefault(c.Id))
            .Select(c => (IReadOnlyList<object?>)new object?[]
            {
                c.Id, c.DisplayName ?? c.LegalName ?? c.FirstName, c.Status, revenueByClient.GetValueOrDefault(c.Id),
            })
            .ToList();

        return new ReportResult(["ClientId", "DisplayName", "Status", "Revenue"], rows);
    }

    // --- Cases/Matters: rpt_fact_matters, grouped by status. ---
    private async Task<ReportResult> RunMattersAsync(Guid tenantId, ReportRunParams p, ReportScope scope, CancellationToken ct)
    {
        var facts = await db.RptFactMatters.Where(f => f.TenantId == tenantId).ToListAsync(ct);
        var filtered = facts
            .Where(f => ReportScopeMatcher.Matches(scope, f.LawyerKey, f.BranchId))
            .Where(f => p.PracticeAreaId is null || f.PracticeAreaKey == p.PracticeAreaId)
            .Where(f => p.LawyerId is null || f.LawyerKey == p.LawyerId)
            .Where(f => p.BranchId is null || f.BranchId == p.BranchId)
            .ToList();

        var rows = filtered
            .GroupBy(f => f.Status ?? "Unknown")
            .Select(g => (IReadOnlyList<object?>)new object?[]
            {
                g.Key,
                g.Count(),
                g.Count(f => f.IsOpen),
                g.Count(f => !f.IsOpen),
                g.Where(f => f.CycleTimeDays.HasValue).Select(f => f.CycleTimeDays!.Value).DefaultIfEmpty(0).Average(),
            })
            .ToList();

        return new ReportResult(["Status", "Count", "OpenCount", "ClosedCount", "AvgCycleTimeDays"], rows);
    }

    // --- Lawyer Performance: rpt_fact_time + rpt_fact_matters, grouped by lawyer. ---
    private async Task<ReportResult> RunLawyerPerformanceAsync(Guid tenantId, ReportRunParams p, ReportScope scope, CancellationToken ct)
    {
        var timeFacts = await db.RptFactTimes.Where(f => f.TenantId == tenantId).ToListAsync(ct);
        var matterFacts = await db.RptFactMatters.Where(f => f.TenantId == tenantId).ToListAsync(ct);

        var scopedTime = timeFacts
            .Where(f => ReportScopeMatcher.Matches(scope, f.LawyerKey, f.BranchId))
            .Where(f => MatchesDateKey(f.DateKey, p))
            .Where(f => p.LawyerId is null || f.LawyerKey == p.LawyerId)
            .Where(f => p.BranchId is null || f.BranchId == p.BranchId)
            .ToList();

        var rows = scopedTime
            .Where(f => f.LawyerKey.HasValue)
            .GroupBy(f => f.LawyerKey!.Value)
            .Select(g => (IReadOnlyList<object?>)new object?[]
            {
                g.Key,
                Math.Round(g.Where(f => f.Billable).Sum(f => f.RoundedMin) / 60m, 2),
                Math.Round(g.Where(f => !f.Billable).Sum(f => f.RoundedMin) / 60m, 2),
                g.Where(f => f.Billable && !f.IsBilled).Sum(f => f.Amount),
                matterFacts.Count(m => m.LawyerKey == g.Key),
            })
            .ToList();

        return new ReportResult(["LawyerKey", "BillableHours", "NonBillableHours", "WipAmount", "MattersHandled"], rows);
    }

    // --- Expenses: legal.matter_expenses (OLTP), scoped via the owning matter's lawyer/branch. ---
    private async Task<ReportResult> RunExpensesAsync(Guid tenantId, ReportRunParams p, ReportScope scope, CancellationToken ct)
    {
        var matterInfo = await db.Matters.Where(m => m.TenantId == tenantId).ToDictionaryAsync(m => m.Id, m => (m.ResponsibleLawyerId, m.BranchId), ct);
        var expenses = await db.MatterExpenses.Where(e => e.TenantId == tenantId).ToListAsync(ct);

        var filtered = expenses
            .Where(e =>
            {
                var (lawyerId, branchId) = matterInfo.GetValueOrDefault(e.MatterId);
                return ReportScopeMatcher.Matches(scope, lawyerId, branchId);
            })
            .Where(e => p.DateFrom is null || e.IncurredOn >= p.DateFrom)
            .Where(e => p.DateTo is null || e.IncurredOn <= p.DateTo)
            .ToList();

        var rows = filtered
            .GroupBy(e => e.Category ?? "Uncategorized")
            .Select(g => (IReadOnlyList<object?>)new object?[]
            {
                g.Key, g.Sum(e => e.Amount), g.Where(e => e.Billable).Sum(e => e.Amount), g.Where(e => !e.Billable).Sum(e => e.Amount),
            })
            .ToList();

        return new ReportResult(["Category", "TotalAmount", "RecoverableAmount", "NonRecoverableAmount"], rows);
    }

    // --- Billing: fin.invoices (OLTP), scoped via the owning matter's lawyer/branch, grouped by status. ---
    private async Task<ReportResult> RunBillingAsync(Guid tenantId, ReportRunParams p, ReportScope scope, CancellationToken ct)
    {
        var matterInfo = await db.Matters.Where(m => m.TenantId == tenantId).ToDictionaryAsync(m => m.Id, m => (m.ResponsibleLawyerId, m.BranchId), ct);
        var invoices = await db.Invoices.Where(i => i.TenantId == tenantId).ToListAsync(ct);

        var filtered = invoices
            .Where(i =>
            {
                var (lawyerId, branchId) = matterInfo.GetValueOrDefault(i.MatterId);
                return ReportScopeMatcher.Matches(scope, lawyerId, branchId);
            })
            .Where(i => p.DateFrom is null || i.IssueDate is null || i.IssueDate >= p.DateFrom)
            .Where(i => p.DateTo is null || i.IssueDate is null || i.IssueDate <= p.DateTo)
            .Where(i => p.ClientId is null || i.ClientId == p.ClientId)
            .ToList();

        var rows = filtered
            .GroupBy(i => i.Status)
            .Select(g => (IReadOnlyList<object?>)new object?[]
            {
                g.Key, g.Count(), g.Sum(i => i.GrandTotal), g.Sum(i => i.AmountPaid), g.Sum(i => i.GrandTotal - i.AmountPaid),
            })
            .ToList();

        return new ReportResult(["Status", "Count", "TotalBilled", "TotalPaid", "TotalOutstanding"], rows);
    }

    // --- Payments: fin.payments (OLTP), scoped via the paying client's owner/branch, grouped by mode. ---
    private async Task<ReportResult> RunPaymentsAsync(Guid tenantId, ReportRunParams p, ReportScope scope, CancellationToken ct)
    {
        var clientInfo = await db.Clients.Where(c => c.TenantId == tenantId).ToDictionaryAsync(c => c.Id, c => (c.OwnerId, c.BranchId), ct);
        var payments = await db.Payments.Where(pay => pay.TenantId == tenantId).ToListAsync(ct);

        var filtered = payments
            .Where(pay =>
            {
                var (ownerId, branchId) = clientInfo.GetValueOrDefault(pay.ClientId);
                return ReportScopeMatcher.Matches(scope, ownerId, branchId);
            })
            .Where(pay => p.DateFrom is null || pay.ReceivedOn >= p.DateFrom)
            .Where(pay => p.DateTo is null || pay.ReceivedOn <= p.DateTo)
            .Where(pay => p.ClientId is null || pay.ClientId == p.ClientId)
            .ToList();

        var rows = filtered
            .GroupBy(pay => pay.Mode)
            .Select(g => (IReadOnlyList<object?>)new object?[]
            {
                g.Key, g.Count(), g.Sum(pay => pay.Amount), g.Count(pay => pay.Status == "Refunded"), g.Count(pay => pay.Status == "Bounced"),
            })
            .ToList();

        return new ReportResult(["Mode", "Count", "TotalAmount", "RefundedCount", "BouncedCount"], rows);
    }

    // --- Tasks: ops.tasks (OLTP), scoped via owner/matter-branch, grouped by status. ---
    private async Task<ReportResult> RunTasksAsync(Guid tenantId, ReportRunParams p, ReportScope scope, CancellationToken ct)
    {
        var matterBranch = await db.Matters.Where(m => m.TenantId == tenantId).ToDictionaryAsync(m => m.Id, m => m.BranchId, ct);
        var tasks = await db.OpsTasks.Where(t => t.TenantId == tenantId).ToListAsync(ct);

        var filtered = tasks
            .Where(t => ReportScopeMatcher.Matches(scope, t.OwnerId, t.MatterId.HasValue ? matterBranch.GetValueOrDefault(t.MatterId.Value) : null))
            .ToList();

        var now = DateTimeOffset.UtcNow;
        var rows = filtered
            .GroupBy(t => t.Status)
            .Select(g => (IReadOnlyList<object?>)new object?[]
            {
                g.Key, g.Count(), g.Count(t => t.Status != "Done" && t.DueAt.HasValue && t.DueAt < now),
            })
            .ToList();

        return new ReportResult(["Status", "Count", "OverdueCount"], rows);
    }

    // --- Hearings: legal.hearings (OLTP), scoped via assigned lawyer/case-matter-branch, grouped by status. ---
    private async Task<ReportResult> RunHearingsAsync(Guid tenantId, ReportRunParams p, ReportScope scope, CancellationToken ct)
    {
        var caseMatter = await db.CourtCases.Where(c => c.TenantId == tenantId).ToDictionaryAsync(c => c.Id, c => c.MatterId, ct);
        var matterBranch = await db.Matters.Where(m => m.TenantId == tenantId).ToDictionaryAsync(m => m.Id, m => m.BranchId, ct);
        var hearings = await db.Hearings.Where(h => h.TenantId == tenantId).ToListAsync(ct);

        var filtered = hearings
            .Where(h =>
            {
                Guid? branchId = caseMatter.TryGetValue(h.CaseId, out var matterId) ? matterBranch.GetValueOrDefault(matterId) : null;
                return ReportScopeMatcher.Matches(scope, h.AssignedLawyerId, branchId);
            })
            .Where(h => p.DateFrom is null || h.Date >= p.DateFrom)
            .Where(h => p.DateTo is null || h.Date <= p.DateTo)
            .ToList();

        var rows = filtered
            .GroupBy(h => h.Status)
            .Select(g => (IReadOnlyList<object?>)new object?[] { g.Key, g.Count() })
            .ToList();

        return new ReportResult(["Status", "Count"], rows);
    }

    // --- Lead Conversion: crm.leads (OLTP), scoped via owner/branch, grouped by source. ---
    private async Task<ReportResult> RunLeadConversionAsync(Guid tenantId, ReportRunParams p, ReportScope scope, CancellationToken ct)
    {
        var leads = await db.Leads.Where(l => l.TenantId == tenantId).ToListAsync(ct);
        var filtered = leads
            .Where(l => ReportScopeMatcher.Matches(scope, l.OwnerId, l.BranchId))
            .Where(l => p.BranchId is null || l.BranchId == p.BranchId)
            .Where(l => p.PracticeAreaId is null || l.PracticeAreaId == p.PracticeAreaId)
            .ToList();

        var rows = filtered
            .GroupBy(l => l.SourceId)
            .Select(g =>
            {
                var count = g.Count();
                var converted = g.Count(l => l.Status == "Converted");
                var rate = count == 0 ? 0 : Math.Round(converted * 100m / count, 2);
                return (IReadOnlyList<object?>)new object?[] { g.Key, count, converted, g.Count(l => l.Status == "Lost"), rate };
            })
            .ToList();

        return new ReportResult(["SourceId", "Count", "ConvertedCount", "LostCount", "ConversionRatePct"], rows);
    }

    // --- Practice Area: rpt_fact_billing + rpt_fact_time + rpt_fact_matters, grouped by practice area. ---
    private async Task<ReportResult> RunPracticeAreaAsync(Guid tenantId, ReportRunParams p, ReportScope scope, CancellationToken ct)
    {
        var billing = await db.RptFactBillings.Where(f => f.TenantId == tenantId).ToListAsync(ct);
        var time = await db.RptFactTimes.Where(f => f.TenantId == tenantId).ToListAsync(ct);
        var matters = await db.RptFactMatters.Where(f => f.TenantId == tenantId).ToListAsync(ct);

        var scopedBilling = billing.Where(f => ReportScopeMatcher.Matches(scope, f.LawyerKey, f.BranchId)).ToList();
        var scopedTime = time.Where(f => ReportScopeMatcher.Matches(scope, f.LawyerKey, f.BranchId)).ToList();
        var scopedMatters = matters.Where(f => ReportScopeMatcher.Matches(scope, f.LawyerKey, f.BranchId)).ToList();

        var practiceAreaKeys = scopedBilling.Select(f => f.PracticeAreaKey)
            .Concat(scopedTime.Select(f => f.PracticeAreaKey))
            .Concat(scopedMatters.Select(f => f.PracticeAreaKey))
            .Where(k => k.HasValue)
            .Select(k => k!.Value)
            .Distinct();

        var rows = practiceAreaKeys
            .Select(key => (IReadOnlyList<object?>)new object?[]
            {
                key,
                scopedBilling.Where(f => f.PracticeAreaKey == key).Sum(f => f.BilledAmount),
                Math.Round(scopedTime.Where(f => f.PracticeAreaKey == key).Sum(f => f.RoundedMin) / 60m, 2),
                scopedMatters.Count(f => f.PracticeAreaKey == key),
            })
            .ToList();

        return new ReportResult(["PracticeAreaKey", "Revenue", "Hours", "MattersCount"], rows);
    }

    // --- Trust: fin.trust_accounts + fin.trust_reconciliations (OLTP), scoped via the client's owner/branch. ---
    private async Task<ReportResult> RunTrustAsync(Guid tenantId, ReportScope scope, CancellationToken ct)
    {
        var clientInfo = await db.Clients.Where(c => c.TenantId == tenantId).ToDictionaryAsync(c => c.Id, c => (c.OwnerId, c.BranchId), ct);
        var accounts = await db.TrustAccounts.Where(a => a.TenantId == tenantId).ToListAsync(ct);
        var latestReconciliation = await db.TrustReconciliations
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.PeriodEnd)
            .FirstOrDefaultAsync(ct);

        var filtered = accounts
            .Where(a =>
            {
                var (ownerId, branchId) = clientInfo.GetValueOrDefault(a.ClientId);
                return ReportScopeMatcher.Matches(scope, ownerId, branchId);
            })
            .ToList();

        var rows = filtered
            .Select(a => (IReadOnlyList<object?>)new object?[]
            {
                a.ClientId, a.CurrentBalance, latestReconciliation?.Status ?? "None", a.CurrentBalance > 0 && a.UpdatedAt < DateTimeOffset.UtcNow.AddDays(-90),
            })
            .ToList();

        return new ReportResult(["ClientId", "CurrentBalance", "LastReconciliationStatus", "DormantFlag"], rows);
    }

    private static bool MatchesDateKey(int? dateKey, ReportRunParams p)
    {
        if (p.DateFrom is null && p.DateTo is null)
        {
            return true;
        }

        if (!dateKey.HasValue)
        {
            return false;
        }

        if (p.DateFrom is not null && dateKey < RptDimDate.KeyFor(p.DateFrom.Value))
        {
            return false;
        }

        if (p.DateTo is not null && dateKey > RptDimDate.KeyFor(p.DateTo.Value))
        {
            return false;
        }

        return true;
    }

    private static string FormatPeriod(int yearMonth) => yearMonth == 0 ? "Unknown" : $"{yearMonth / 100:D4}-{yearMonth % 100:D2}";
}
