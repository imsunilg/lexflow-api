using System.Text.Json;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Fin;

/// <summary>
/// Module 8 User Flow #7: dunning reminder schedule (default steps: due-3, due, +7, +15, +30 days;
/// escalation to lawyer at +30) + per-invoice mute. <see cref="RunDueRemindersAsync"/> is the
/// Hangfire recurring-job entry point (see LexFlow.Workers/Program.cs) — for every outstanding
/// invoice it ensures the tenant's active schedule's steps exist as ops.fin.dunning_events rows,
/// then "sends" (Notification stub — the concrete channel fan-out lives in Module 6/10's
/// INotificationService, not duplicated here) any that are now due and unmuted.
/// </summary>
public sealed class DunningService(LexFlowDbContext db) : IDunningService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task RunDueRemindersAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var invoices = await db.Invoices.Where(i => i.Status == "Sent" || i.Status == "PartiallyPaid" || i.Status == "Overdue").ToListAsync(cancellationToken);

        foreach (var invoice in invoices)
        {
            if (invoice.DueDate is null)
            {
                continue;
            }

            var today = DateOnly.FromDateTime(now.UtcDateTime);
            if (invoice.DueDate < today && invoice.Status != "Overdue")
            {
                invoice.MarkOverdue();
            }

            var schedule = await db.DunningSchedules.SingleOrDefaultAsync(s => s.TenantId == invoice.TenantId && s.IsActive, cancellationToken);
            if (schedule is null)
            {
                continue;
            }

            await EnsureScheduledEventsAsync(invoice, schedule, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        var due = await db.DunningEvents.Where(e => e.Status == "Pending" && !e.Muted && e.ScheduledFor <= now).ToListAsync(cancellationToken);
        foreach (var dunningEvent in due)
        {
            // Concrete channel delivery (Email/SMS/WhatsApp/Portal) is Module 6/10/§22's
            // INotificationService fan-out — this job's own responsibility ends at "this reminder
            // is now due"; wiring the actual send is left to the caller/worker composition, same
            // separation already established for ReminderDispatchService in this codebase.
            dunningEvent.MarkSent();
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MuteAsync(Guid tenantId, Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var events = await db.DunningEvents.Where(e => e.TenantId == tenantId && e.InvoiceId == invoiceId && e.Status == "Pending").ToListAsync(cancellationToken);
        foreach (var dunningEvent in events)
        {
            dunningEvent.Mute();
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<DunningScheduleDto> UpsertScheduleAsync(Guid tenantId, string name, string stepsJson, bool isActive, CancellationToken cancellationToken = default)
    {
        var existing = await db.DunningSchedules.SingleOrDefaultAsync(s => s.TenantId == tenantId && s.Name == name, cancellationToken);
        if (existing is not null)
        {
            existing.Update(name, stepsJson, isActive);
            await db.SaveChangesAsync(cancellationToken);
            return ToDto(existing);
        }

        var schedule = new DunningSchedule(tenantId, name, stepsJson);
        await db.DunningSchedules.AddAsync(schedule, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(schedule);
    }

    public async Task<IReadOnlyList<DunningScheduleDto>> GetSchedulesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var schedules = await db.DunningSchedules.Where(s => s.TenantId == tenantId).ToListAsync(cancellationToken);
        return schedules.Select(ToDto).ToList();
    }

    /// <summary>Steps JSON shape: [{ "label": "due-3", "offsetDays": -3, "channel": "Email" }, ...] — offsetDays relative to the invoice's due date; +30 is the documented "escalation to lawyer" step (Module 8 User Flow #7), surfaced by StepLabel for the caller to branch on.</summary>
    private async Task EnsureScheduledEventsAsync(Invoice invoice, DunningSchedule schedule, CancellationToken cancellationToken)
    {
        if (JsonSerializer.Deserialize<List<DunningStep>>(schedule.StepsJson, JsonOptions) is not { } steps)
        {
            return;
        }

        var existingLabels = await db.DunningEvents.Where(e => e.InvoiceId == invoice.Id && e.ScheduleId == schedule.Id).Select(e => e.StepLabel).ToListAsync(cancellationToken);

        foreach (var step in steps)
        {
            if (existingLabels.Contains(step.Label))
            {
                continue;
            }

            var scheduledFor = invoice.DueDate!.Value.AddDays(step.OffsetDays).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            await db.DunningEvents.AddAsync(new DunningEvent(invoice.TenantId, invoice.Id, schedule.Id, step.Label, step.Channel, scheduledFor), cancellationToken);
        }
    }

    private static DunningScheduleDto ToDto(DunningSchedule s) => new(s.Id, s.Name, s.StepsJson, s.IsActive);

    private sealed record DunningStep(string Label, int OffsetDays, string Channel);
}
