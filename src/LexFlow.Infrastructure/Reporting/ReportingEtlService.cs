using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Reporting;

/// <summary>
/// Module 13 Database: "heavy aggregates from nightly-built star-schema tables ... (refreshed
/// hourly incremental)." <see cref="RunIncrementalAsync"/> is the Hangfire recurring-job entry
/// point (see LexFlow.Workers/Program.cs), same cross-tenant iteration style as
/// DunningService.RunDueRemindersAsync/ReminderDispatchService in this codebase (no explicit
/// tenant_id filter — every row already carries its own TenantId, and the job upserts across all
/// tenants in one pass).
///
/// Every dim/fact row is upserted keyed by its source id (never hard-deleted — see each
/// rpt.rpt_dim_*/rpt_fact_* table's own 001_Table.sql "deleted lawyer in history" edge case), so
/// the whole method is safe to run hourly and safe to re-run. It re-upserts every source row on
/// every run rather than tracking a "changed since" watermark — a true delta-only incremental
/// (only rows touched since the last run) is the natural next step once a job-run ledger exists,
/// but a full upsert is idempotent and correct, just not minimal, which is the right tradeoff
/// given this table set's size in practice.
/// </summary>
public sealed class ReportingEtlService(LexFlowDbContext db) : IReportingEtlService
{
    public async Task RunIncrementalAsync(CancellationToken cancellationToken = default)
    {
        await UpsertDimDatesAsync(cancellationToken);
        await UpsertDimLawyersAsync(cancellationToken);
        await UpsertDimClientsAsync(cancellationToken);
        await UpsertDimPracticeAreasAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await UpsertFactBillingAsync(cancellationToken);
        await UpsertFactTimeAsync(cancellationToken);
        await UpsertFactMattersAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertDimDatesAsync(CancellationToken cancellationToken)
    {
        var existingKeys = await db.RptDimDates.Select(d => d.DateKey).ToListAsync(cancellationToken);
        var existing = new HashSet<int>(existingKeys);

        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-3);
        var end = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1);

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var key = RptDimDate.KeyFor(date);
            if (!existing.Contains(key))
            {
                await db.RptDimDates.AddAsync(new RptDimDate(key, date), cancellationToken);
                existing.Add(key);
            }
        }
    }

    private async Task UpsertDimLawyersAsync(CancellationToken cancellationToken)
    {
        var users = await db.Users.ToListAsync(cancellationToken);
        var existing = await db.RptDimLawyers.ToDictionaryAsync(l => l.LawyerKey, cancellationToken);

        foreach (var user in users)
        {
            var isActive = user.Status == LexFlow.Domain.Enums.UserStatus.Active;
            if (existing.TryGetValue(user.Id, out var dim))
            {
                dim.Upsert(user.Name, null, user.BranchId, user.DepartmentId, isActive);
            }
            else
            {
                await db.RptDimLawyers.AddAsync(new RptDimLawyer(user.Id, user.TenantId, user.Name, null, user.BranchId, user.DepartmentId, isActive), cancellationToken);
            }
        }
    }

    private async Task UpsertDimClientsAsync(CancellationToken cancellationToken)
    {
        var clients = await db.Clients.ToListAsync(cancellationToken);
        var existing = await db.RptDimClients.ToDictionaryAsync(c => c.ClientKey, cancellationToken);

        foreach (var client in clients)
        {
            var displayName = client.DisplayName ?? client.LegalName ?? client.FirstName ?? client.Number;
            var isActive = client.Status == "Active";
            if (existing.TryGetValue(client.Id, out var dim))
            {
                dim.Upsert(displayName, client.Type, client.BranchId, isActive);
            }
            else
            {
                await db.RptDimClients.AddAsync(new RptDimClient(client.Id, client.TenantId, displayName, client.Type, client.BranchId, isActive), cancellationToken);
            }
        }
    }

    private async Task UpsertDimPracticeAreasAsync(CancellationToken cancellationToken)
    {
        var practiceAreas = await db.PracticeAreas.ToListAsync(cancellationToken);
        var existing = await db.RptDimPracticeAreas.ToDictionaryAsync(p => p.PracticeAreaKey, cancellationToken);

        foreach (var practiceArea in practiceAreas)
        {
            if (existing.TryGetValue(practiceArea.Id, out var dim))
            {
                dim.Upsert(practiceArea.Name, practiceArea.ParentId, practiceArea.IsActive);
            }
            else
            {
                await db.RptDimPracticeAreas.AddAsync(new RptDimPracticeArea(practiceArea.Id, practiceArea.TenantId, practiceArea.Name, practiceArea.ParentId, practiceArea.IsActive), cancellationToken);
            }
        }
    }

    private async Task UpsertFactBillingAsync(CancellationToken cancellationToken)
    {
        var matters = await db.Matters.ToDictionaryAsync(m => m.Id, cancellationToken);
        var invoices = await db.Invoices.ToListAsync(cancellationToken);
        var existing = await db.RptFactBillings.ToDictionaryAsync(f => f.InvoiceId, cancellationToken);

        foreach (var invoice in invoices)
        {
            matters.TryGetValue(invoice.MatterId, out var matter);
            var dateKey = invoice.IssueDate.HasValue ? RptDimDate.KeyFor(invoice.IssueDate.Value) : (int?)null;
            var outstanding = invoice.GrandTotal - invoice.AmountPaid;
            var writeOff = invoice.Status == "Void" ? invoice.GrandTotal - invoice.AmountPaid : 0m;

            if (existing.TryGetValue(invoice.Id, out var fact))
            {
                fact.Apply(dateKey, matter?.ResponsibleLawyerId, invoice.ClientId, matter?.PracticeAreaId, matter?.BranchId, invoice.GrandTotal, invoice.AmountPaid, writeOff, outstanding, invoice.TaxTotal);
            }
            else
            {
                await db.RptFactBillings.AddAsync(new RptFactBilling(invoice.TenantId, invoice.Id, dateKey, matter?.ResponsibleLawyerId, invoice.ClientId, matter?.PracticeAreaId, matter?.BranchId, invoice.GrandTotal, invoice.AmountPaid, writeOff, outstanding, invoice.TaxTotal), cancellationToken);
            }
        }
    }

    private async Task UpsertFactTimeAsync(CancellationToken cancellationToken)
    {
        var matters = await db.Matters.ToDictionaryAsync(m => m.Id, cancellationToken);
        var entries = await db.TimeEntries.ToListAsync(cancellationToken);
        var existing = await db.RptFactTimes.ToDictionaryAsync(f => f.TimeEntryId, cancellationToken);

        foreach (var entry in entries)
        {
            matters.TryGetValue(entry.MatterId, out var matter);
            var dateKey = RptDimDate.KeyFor(entry.EntryDate);
            var isBilled = entry.Status == "Billed";
            var amount = entry.AmountSnapshot ?? 0m;

            if (existing.TryGetValue(entry.Id, out var fact))
            {
                fact.Apply(dateKey, entry.UserId, matter?.ClientId, matter?.PracticeAreaId, matter?.BranchId, entry.MatterId, entry.DurationMin, entry.RoundedMin, entry.Billable, isBilled, amount);
            }
            else
            {
                await db.RptFactTimes.AddAsync(new RptFactTime(entry.TenantId, entry.Id, dateKey, entry.UserId, matter?.ClientId, matter?.PracticeAreaId, matter?.BranchId, entry.MatterId, entry.DurationMin, entry.RoundedMin, entry.Billable, isBilled, amount), cancellationToken);
            }
        }
    }

    private async Task UpsertFactMattersAsync(CancellationToken cancellationToken)
    {
        var matters = await db.Matters.ToListAsync(cancellationToken);
        var existing = await db.RptFactMatters.ToDictionaryAsync(f => f.MatterId, cancellationToken);

        foreach (var matter in matters)
        {
            var openedKey = RptDimDate.KeyFor(matter.OpenedOn);
            var closedKey = matter.ClosedOn.HasValue ? RptDimDate.KeyFor(matter.ClosedOn.Value) : (int?)null;
            var cycleTimeDays = matter.ClosedOn.HasValue ? matter.ClosedOn.Value.DayNumber - matter.OpenedOn.DayNumber : (int?)null;
            var isOpen = !matter.ClosedOn.HasValue;

            if (existing.TryGetValue(matter.Id, out var fact))
            {
                fact.Apply(openedKey, closedKey, matter.ResponsibleLawyerId, matter.ClientId, matter.PracticeAreaId, matter.BranchId, matter.Status, matter.Outcome, cycleTimeDays, isOpen);
            }
            else
            {
                await db.RptFactMatters.AddAsync(new RptFactMatters(matter.TenantId, matter.Id, openedKey, closedKey, matter.ResponsibleLawyerId, matter.ClientId, matter.PracticeAreaId, matter.BranchId, matter.Status, matter.Outcome, cycleTimeDays, isOpen), cancellationToken);
            }
        }
    }
}
