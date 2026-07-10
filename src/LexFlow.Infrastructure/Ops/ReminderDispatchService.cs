using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Ops;

/// <summary>
/// AC-CAL2: "Same-day 07:00 hearing reminder delivers on all enabled channels; dispatch
/// log proves it." A Hangfire recurring job calls <see cref="DispatchDueAsync"/>; for each
/// Pending ops.event_reminders row it resolves the referenced entity's due time and
/// recipient (polymorphic on event_ref_kind — hearing/event/deadline/task each have a
/// different "who gets notified" and "when is it due" rule), fires through
/// INotificationService once the offset window is reached, and writes an
/// ops.reminder_dispatch_log row per channel actually attempted regardless of outcome.
/// </summary>
public sealed class ReminderDispatchService(LexFlowDbContext db, INotificationService notificationService)
{
    public async Task DispatchDueAsync(CancellationToken cancellationToken = default)
    {
        var pending = await db.EventReminders.Where(r => r.Status == "Pending").ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        foreach (var reminder in pending)
        {
            var target = await ResolveTargetAsync(reminder, cancellationToken);
            if (target is null)
            {
                continue;
            }

            var (dueAt, userId, title) = target.Value;
            var triggerAt = dueAt.AddMinutes(-reminder.OffsetMinutes);
            if (now < triggerAt)
            {
                continue;
            }

            // Hearing day-of reminders (offset 0) are the mandatory class — §22: "hearing
            // day-of ignores quiet hours."
            var mandatory = reminder.EventRefKind == "hearing" && reminder.OffsetMinutes == 0;

            string status;
            string? error = null;
            try
            {
                await notificationService.NotifyAsync(reminder.TenantId, userId, new NotifyRequest(
                    Kind: $"reminder.{reminder.EventRefKind}",
                    Title: title,
                    Body: null,
                    DeepLink: null,
                    Channels: [reminder.Channel],
                    Mandatory: mandatory), cancellationToken);
                status = "Sent";
            }
            catch (Exception ex)
            {
                status = "Failed";
                error = ex.Message;
            }

            await db.ReminderDispatchLogs.AddAsync(new ReminderDispatchLog(reminder.TenantId, reminder.Id, reminder.Channel, status, providerRef: null, error), cancellationToken);

            if (status == "Sent")
            {
                reminder.MarkSent();
            }
            else
            {
                reminder.MarkFailed();
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<(DateTimeOffset DueAt, Guid UserId, string Title)?> ResolveTargetAsync(EventReminder reminder, CancellationToken cancellationToken)
    {
        switch (reminder.EventRefKind)
        {
            case "hearing":
            {
                var hearing = await db.Hearings.SingleOrDefaultAsync(h => h.TenantId == reminder.TenantId && h.Id == reminder.EventRefId, cancellationToken);
                if (hearing?.AssignedLawyerId is null)
                {
                    return null;
                }

                var dueAt = new DateTimeOffset(hearing.Date.ToDateTime(hearing.Time ?? new TimeOnly(7, 0)), TimeSpan.Zero);
                return (dueAt, hearing.AssignedLawyerId.Value, $"Hearing reminder: {hearing.Purpose ?? "Hearing"}");
            }

            case "event":
            {
                var calendarEvent = await db.CalendarEvents.SingleOrDefaultAsync(e => e.TenantId == reminder.TenantId && e.Id == reminder.EventRefId, cancellationToken);
                if (calendarEvent?.OrganizerId is null)
                {
                    return null;
                }

                return (calendarEvent.StartsAt, calendarEvent.OrganizerId.Value, $"Event reminder: {calendarEvent.Title}");
            }

            case "deadline":
            {
                var deadline = await db.MatterImportantDates.SingleOrDefaultAsync(d => d.TenantId == reminder.TenantId && d.Id == reminder.EventRefId, cancellationToken);
                if (deadline is null || deadline.SatisfiedAt is not null)
                {
                    return null;
                }

                var matter = await db.Matters.SingleOrDefaultAsync(m => m.TenantId == reminder.TenantId && m.Id == deadline.MatterId, cancellationToken);
                if (matter?.ResponsibleLawyerId is null)
                {
                    return null;
                }

                return (deadline.DueAt, matter.ResponsibleLawyerId.Value, $"Deadline reminder: {deadline.Title}");
            }

            case "task":
            {
                var task = await db.OpsTasks.SingleOrDefaultAsync(t => t.TenantId == reminder.TenantId && t.Id == reminder.EventRefId, cancellationToken);
                if (task?.OwnerId is null || task.DueAt is null)
                {
                    return null;
                }

                return (task.DueAt.Value, task.OwnerId.Value, $"Task reminder: {task.Title}");
            }

            default:
                return null;
        }
    }
}
