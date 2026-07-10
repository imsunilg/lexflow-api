namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to ops.task_assignees (lexflow-database Scripts/07_Ops/TaskAssignees).
/// Composite-PK join table (task_id, user_id) — see MatterTeamMember for why this does not
/// derive from Entity/AuditableEntity.
/// </summary>
public sealed class TaskAssignee
{
    private TaskAssignee()
    {
    }

    public TaskAssignee(Guid tenantId, Guid taskId, Guid userId, string role)
    {
        TenantId = tenantId;
        TaskId = taskId;
        UserId = userId;
        Role = role;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid TenantId { get; private set; }
    public Guid TaskId { get; private set; }
    public Guid UserId { get; private set; }
    public string Role { get; private set; } = "collaborator";
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
    }
}
