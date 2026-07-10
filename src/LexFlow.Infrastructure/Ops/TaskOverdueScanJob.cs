using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Ops;

/// <summary>
/// §22/§23: "Task assigned / due / overdue ... overdue+24h -> manager"; shipped default
/// rule #4 ("Task overdue -> assignee nudge; +24h -> manager"). ops.tasks has no
/// event to hook (overdue is a schedule-based state, not a mutation), so a Hangfire
/// recurring job scans for newly-overdue tasks and publishes "task.overdue" through the
/// same outbox the mutation-triggered events use, keeping the rule engine's evaluation
/// path uniform regardless of trigger source.
/// </summary>
public sealed class TaskOverdueScanJob(LexFlowDbContext db, IWorkflowEventPublisher workflowEvents)
{
    public async Task ScanAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var overdueTasks = await db.OpsTasks
            .Where(t => t.DueAt != null && t.DueAt < now && t.Status != "Done" && t.Status != "Cancelled")
            .ToListAsync(cancellationToken);

        foreach (var task in overdueTasks)
        {
            var overdueHours = (int)(now - task.DueAt!.Value).TotalHours;

            // Fires once near "just became overdue" and once near "+24h -> escalate to
            // manager" (§22 timing column), assuming an hourly job cadence — not a
            // per-run re-notification for every overdue task on every scan.
            if (overdueHours is not (0 or 24))
            {
                continue;
            }

            await workflowEvents.PublishAsync(task.TenantId, "task.overdue", task.Id, new
            {
                entityId = task.Id,
                ownerId = task.OwnerId,
                matterId = task.MatterId,
                overdueHours,
            }, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
