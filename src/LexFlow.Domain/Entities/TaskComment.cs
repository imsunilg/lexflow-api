using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to ops.task_comments (lexflow-database Scripts/07_Ops/TaskComments).</summary>
public sealed class TaskComment : AuditableEntity
{
    private TaskComment()
    {
    }

    public TaskComment(Guid tenantId, Guid taskId, Guid? authorId, string body)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        TaskId = taskId;
        AuthorId = authorId;
        Body = body;
    }

    public Guid TaskId { get; private set; }
    public Guid? AuthorId { get; private set; }
    public string Body { get; private set; } = null!;
}
