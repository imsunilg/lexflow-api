using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to ops.tasks (lexflow-database Scripts/07_Ops/Tasks). Named
/// "OpsTask" (not "Task") to avoid colliding with System.Threading.Tasks.Task. Originally
/// modeled with just the minimal surface Module 5's "one-click create compliance task"
/// needed; extended here with the full Module 10 task-management vertical slice
/// (status workflow, dependencies via TaskDependency, checklist-gated Done, templates).
/// </summary>
public sealed class OpsTask : AuditableEntity
{
    private OpsTask()
    {
    }

    public OpsTask(Guid tenantId, string title, string? description, Guid? matterId, Guid? clientId, Guid? ownerId, DateTimeOffset? dueAt, string priority, string? category, Guid? recurrenceId = null, string? templateKey = null)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Title = title;
        Description = description;
        MatterId = matterId;
        ClientId = clientId;
        OwnerId = ownerId;
        DueAt = dueAt;
        Priority = priority;
        Category = category;
        RecurrenceId = recurrenceId;
        TemplateKey = templateKey;
        Status = "New";
        ProgressPct = 0;
    }

    public string? Number { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public Guid? MatterId { get; private set; }
    public Guid? ClientId { get; private set; }
    public Guid? OwnerId { get; private set; }
    public DateTimeOffset? DueAt { get; private set; }
    public string Priority { get; private set; } = "Medium";
    public string? Category { get; private set; }
    public string Status { get; private set; } = "New";
    public int ProgressPct { get; private set; }
    public Guid? RecurrenceId { get; private set; }
    public string? TemplateKey { get; private set; }

    public void UpdateDetails(string title, string? description, Guid? matterId, Guid? clientId, Guid? ownerId, DateTimeOffset? dueAt, string priority, string? category)
    {
        Title = title;
        Description = description;
        MatterId = matterId;
        ClientId = clientId;
        OwnerId = ownerId;
        DueAt = dueAt;
        Priority = priority;
        Category = category;
    }

    /// <summary>AC-TK2: blocked (open dependency) can never move to InProgress; status-jump validity is enforced by the caller.</summary>
    public void SetStatus(string status) => Status = status;

    public void SetProgressPct(int progressPct) => ProgressPct = Math.Clamp(progressPct, 0, 100);
}
