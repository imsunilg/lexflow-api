using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Dashboard;

/// <summary>
/// Module 1 dashboard — backs GET /api/v1/dashboard/layout (+PUT) and every
/// GET /api/v1/dashboard/widgets/* route. Layout has no dedicated persistence
/// table yet (no core.user_dashboard_layouts migration exists in
/// lexflow-database), so GetLayoutAsync always returns the widget catalog's
/// static default and SaveLayoutAsync is a validating no-op that echoes the
/// input back — enough for the frontend's "no saved layout yet -> fall back to
/// default" path (DashboardLayoutService's 404 handling) to work today without
/// inventing a schema out of band from the DB team's migrations.
/// </summary>
public sealed class DashboardService(LexFlowDbContext db) : IDashboardService
{
    private static readonly string[] DefaultWidgetOrder =
    [
        "hearings-today", "tasks-pending", "revenue", "outstanding", "deadlines", "activity",
        "case-stats", "lawyer-performance", "matter-summary", "client-summary", "lead-pipeline", "trust-balance",
    ];

    private static readonly Dictionary<string, string> DefaultWidgetSizes = new()
    {
        ["hearings-today"] = "medium",
        ["tasks-pending"] = "medium",
        ["revenue"] = "large",
        ["outstanding"] = "medium",
        ["deadlines"] = "medium",
        ["activity"] = "medium",
        ["case-stats"] = "medium",
        ["lawyer-performance"] = "large",
        ["matter-summary"] = "medium",
        ["client-summary"] = "small",
        ["lead-pipeline"] = "small",
        ["trust-balance"] = "small",
    };

    public Task<DashboardLayoutDto> GetLayoutAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult(DefaultLayout());

    public Task<DashboardLayoutDto> SaveLayoutAsync(Guid tenantId, Guid userId, DashboardLayoutDto layout, CancellationToken cancellationToken = default)
        => Task.FromResult(layout);

    private static DashboardLayoutDto DefaultLayout()
        => new(DefaultWidgetOrder.Select((id, index) => new DashboardWidgetLayoutEntryDto(id, DefaultWidgetSizes[id], index, true)).ToList());

    public async Task<IReadOnlyList<HearingTodayItemDto>> GetHearingsTodayAsync(Guid tenantId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var rows = await (
            from h in db.Hearings
            where h.TenantId == tenantId && h.Date == date
            join cc in db.CourtCases on h.CaseId equals cc.Id
            join m in db.Matters on cc.MatterId equals m.Id
            join cl in db.Clients on m.ClientId equals cl.Id
            join court in db.Courts on cc.CourtId equals court.Id
            select new { h, cc, m, cl, court })
            .OrderBy(x => x.h.Time)
            .ToListAsync(cancellationToken);

        var lawyerIds = rows.Where(x => x.h.AssignedLawyerId.HasValue).Select(x => x.h.AssignedLawyerId!.Value).Distinct().ToList();
        var lawyers = await db.Users.Where(u => lawyerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        return rows.Select(x => new HearingTodayItemDto(
            x.h.Id,
            x.h.Time?.ToString("HH:mm"),
            x.court.Name,
            x.cc.CaseNumber,
            x.m.Title,
            x.cl.DisplayName ?? x.cl.LegalName ?? $"{x.cl.FirstName} {x.cl.LastName}".Trim(),
            x.h.AssignedLawyerId.HasValue && lawyers.TryGetValue(x.h.AssignedLawyerId.Value, out var name) ? name : null))
            .ToList();
    }

    public async Task<IReadOnlyList<TaskPendingItemDto>> GetTasksPendingAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var assignedTaskIds = await db.TaskAssignees.Where(a => a.TenantId == tenantId && a.UserId == userId).Select(a => a.TaskId).ToListAsync(cancellationToken);

        var pendingStatuses = new[] { "New", "InProgress", "InReview" };
        var tasks = await db.OpsTasks
            .Where(t => t.TenantId == tenantId && pendingStatuses.Contains(t.Status)
                && (t.OwnerId == userId || assignedTaskIds.Contains(t.Id)))
            .OrderBy(t => t.DueAt)
            .ToListAsync(cancellationToken);

        var matterIds = tasks.Where(t => t.MatterId.HasValue).Select(t => t.MatterId!.Value).Distinct().ToList();
        var matterTitles = await db.Matters.Where(m => matterIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Title, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return tasks.Select(t =>
        {
            var dueDate = t.DueAt.HasValue ? DateOnly.FromDateTime(t.DueAt.Value.UtcDateTime) : (DateOnly?)null;
            var bucket = dueDate is null ? "thisWeek"
                : dueDate < today ? "overdue"
                : dueDate == today ? "today"
                : "thisWeek";
            return new TaskPendingItemDto(
                t.Id,
                t.Title,
                t.DueAt?.ToString("yyyy-MM-dd") ?? string.Empty,
                bucket,
                t.MatterId.HasValue && matterTitles.TryGetValue(t.MatterId.Value, out var title) ? title : null);
        }).ToList();
    }

    public async Task<IReadOnlyList<DeadlineItemDto>> GetDeadlinesAsync(Guid tenantId, int days, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = today.AddDays(days);

        var hearingRows = await (
            from h in db.Hearings
            where h.TenantId == tenantId && h.Status == "Scheduled" && h.Date >= today && h.Date <= horizon
            join cc in db.CourtCases on h.CaseId equals cc.Id
            join m in db.Matters on cc.MatterId equals m.Id
            select new { h, m })
            .ToListAsync(cancellationToken);

        var deadlines = hearingRows.Select(x => new DeadlineItemDto(
            x.h.Id,
            $"Hearing: {x.h.Purpose ?? x.m.Title}",
            x.h.Date.ToString("yyyy-MM-dd"),
            SeverityForDate(x.h.Date, today),
            x.m.Title)).ToList();

        var now = DateTimeOffset.UtcNow;
        var horizonOffset = new DateTimeOffset(horizon.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
        var pendingStatuses = new[] { "New", "InProgress", "InReview" };
        var taskRows = await db.OpsTasks
            .Where(t => t.TenantId == tenantId && pendingStatuses.Contains(t.Status) && t.DueAt != null && t.DueAt <= horizonOffset)
            .ToListAsync(cancellationToken);

        var matterIds = taskRows.Where(t => t.MatterId.HasValue).Select(t => t.MatterId!.Value).Distinct().ToList();
        var matterTitles = await db.Matters.Where(m => matterIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Title, cancellationToken);

        deadlines.AddRange(taskRows.Select(t => new DeadlineItemDto(
            t.Id,
            t.Title,
            t.DueAt!.Value.ToString("yyyy-MM-dd"),
            t.Priority is "Urgent" or "High" ? "high" : t.Priority == "Medium" ? "medium" : "low",
            t.MatterId.HasValue && matterTitles.TryGetValue(t.MatterId.Value, out var title) ? title : null)));

        return deadlines.OrderBy(d => d.DueDate).ToList();
    }

    private static string SeverityForDate(DateOnly date, DateOnly today)
    {
        var daysOut = date.DayNumber - today.DayNumber;
        return daysOut <= 2 ? "high" : daysOut <= 7 ? "medium" : "low";
    }

    public async Task<IReadOnlyList<ActivityItemDto>> GetActivityAsync(Guid tenantId, Guid userId, int limit, CancellationToken cancellationToken = default)
    {
        var clearedAt = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.DashboardActivityClearedAt)
            .SingleOrDefaultAsync(cancellationToken);

        var events = await db.AuditEvents
            .Where(a => a.TenantId == tenantId && (clearedAt == null || a.At > clearedAt))
            .OrderByDescending(a => a.At)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var actorIds = events.Where(a => a.ActorUserId.HasValue).Select(a => a.ActorUserId!.Value).Distinct().ToList();
        var actors = await db.Users.Where(u => actorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        return events.Select(a => new ActivityItemDto(
            a.Id,
            $"{a.Action} {HumanizeEntityType(a.EntityType)}",
            a.ActorUserId.HasValue && actors.TryGetValue(a.ActorUserId.Value, out var name) ? name : "System",
            a.At)).ToList();
    }

    /// <summary>
    /// "Clear All" on the Recent Activities widget. audit.audit_events is insert-only
    /// (grant-revoked and trigger-blocked against UPDATE/DELETE — PRD §18/§30) and is the
    /// tenant's shared audit trail, not a per-user feed, so this never touches it: it only
    /// advances this user's own cursor, which <see cref="GetActivityAsync"/> filters against.
    /// </summary>
    public async Task ClearActivityAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleAsync(u => u.TenantId == tenantId && u.Id == userId, cancellationToken);
        user.ClearDashboardActivity(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string HumanizeEntityType(string entityType)
    {
        var lastDot = entityType.LastIndexOf('.');
        return lastDot >= 0 ? entityType[(lastDot + 1)..].Replace('_', ' ') : entityType;
    }

    public async Task<CaseStatsSummaryDto> GetCaseStatsAsync(Guid tenantId, DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        var matters = await db.Matters
            .Where(m => m.TenantId == tenantId && m.OpenedOn >= start && m.OpenedOn <= end)
            .ToListAsync(cancellationToken);

        var matterIds = matters.Select(m => m.Id).ToList();
        var stageCounts = await db.CourtCases
            .Where(cc => cc.TenantId == tenantId && matterIds.Contains(cc.MatterId) && cc.Stage != null)
            .GroupBy(cc => cc.Stage)
            .Select(g => new CaseStatsStageDto(g.Key!, g.Count()))
            .ToListAsync(cancellationToken);

        return new CaseStatsSummaryDto(
            matters.Count(m => m.Status == "Open"),
            matters.Count(m => m.Status == "Closed"),
            matters.Count(m => m.Outcome == "Won"),
            matters.Count(m => m.Outcome == "Lost"),
            matters.Count(m => m.Outcome == "Settled"),
            stageCounts);
    }

    public async Task<IReadOnlyList<LawyerPerformanceItemDto>> GetLawyerPerformanceAsync(Guid tenantId, DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        var entries = await db.TimeEntries
            .Where(t => t.TenantId == tenantId && t.EntryDate >= start && t.EntryDate <= end)
            .ToListAsync(cancellationToken);

        if (entries.Count == 0)
        {
            return [];
        }

        var lawyerIds = entries.Select(e => e.UserId).Distinct().ToList();
        var lawyers = await db.Users.Where(u => lawyerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        var businessDays = Math.Max(1, CountBusinessDays(start, end));
        var capacityHours = businessDays * 8m;

        return entries
            .GroupBy(e => e.UserId)
            .Select(g =>
            {
                var billableMinutes = g.Where(e => e.Billable).Sum(e => e.RoundedMin);
                var billableHours = Math.Round(billableMinutes / 60m, 1);
                var utilization = capacityHours > 0 ? Math.Round(Math.Min(100m, billableHours / capacityHours * 100m), 1) : 0m;

                var standardValue = g.Where(e => e.Billable && e.RateSnapshot.HasValue).Sum(e => e.RateSnapshot!.Value * e.RoundedMin / 60m);
                var billedValue = g.Where(e => e.Status is "Billed" or "Approved").Sum(e => e.AmountSnapshot ?? 0m);
                var realization = standardValue > 0 ? Math.Round(Math.Min(100m, billedValue / standardValue * 100m), 1) : 0m;

                return new LawyerPerformanceItemDto(
                    g.Key,
                    lawyers.TryGetValue(g.Key, out var name) ? name : "Unknown",
                    billableHours,
                    utilization,
                    realization);
            })
            .OrderByDescending(x => x.BillableHours)
            .ToList();
    }

    public async Task<IReadOnlyList<MatterSummaryItemDto>> GetMatterSummaryAsync(Guid tenantId, DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        var rows = await (
            from m in db.Matters
            where m.TenantId == tenantId && m.OpenedOn >= start && m.OpenedOn <= end
            join pa in db.PracticeAreas on m.PracticeAreaId equals pa.Id into paJoin
            from pa in paJoin.DefaultIfEmpty()
            group m by new { m.Status, PracticeArea = pa != null ? pa.Name : m.MatterType } into g
            select new MatterSummaryItemDto(g.Key.Status, g.Key.PracticeArea, g.Count()))
            .ToListAsync(cancellationToken);

        return rows.OrderByDescending(r => r.Count).ToList();
    }

    public async Task<ClientSummaryWidgetDataDto> GetClientSummaryAsync(Guid tenantId, DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        var startOffset = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var endOffset = new DateTimeOffset(end.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        var newThisMonth = await db.Clients.CountAsync(c => c.TenantId == tenantId && c.CreatedAt >= startOffset && c.CreatedAt <= endOffset, cancellationToken);
        var active = await db.Clients.CountAsync(c => c.TenantId == tenantId && c.Status == "Active", cancellationToken);

        // "At risk" = active clients with at least one Overdue invoice — the closest signal
        // available from seeded data to a real churn-risk score (no such scoring exists yet).
        var atRisk = await db.Invoices
            .Where(i => i.TenantId == tenantId && i.Status == "Overdue")
            .Select(i => i.ClientId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new ClientSummaryWidgetDataDto(newThisMonth, active, atRisk);
    }

    public async Task<IReadOnlyList<LeadPipelineStageDto>> GetLeadPipelineAsync(Guid tenantId, DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        var startOffset = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var endOffset = new DateTimeOffset(end.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        var rows = await db.Leads
            .Where(l => l.TenantId == tenantId && l.CreatedAt >= startOffset && l.CreatedAt <= endOffset)
            .GroupBy(l => l.Stage)
            .Select(g => new LeadPipelineStageDto(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        var stageOrder = new[] { "New", "Contacted", "Qualified", "Proposal Sent", "Consultation Scheduled", "Consultation Done", "Converted", "Lost" };
        return rows.OrderBy(r => Array.IndexOf(stageOrder, r.Stage) is var idx && idx >= 0 ? idx : int.MaxValue).ToList();
    }

    private static int CountBusinessDays(DateOnly start, DateOnly end)
    {
        var count = 0;
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                count++;
            }
        }

        return count;
    }
}
