using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to ops.task_checklist_items (lexflow-database
/// Scripts/07_Ops/TaskChecklistItems). Module 10: "done requires all 'mandatory' checklist
/// items ticked (flag per item)."
/// </summary>
public sealed class TaskChecklistItem : AuditableEntity
{
    private TaskChecklistItem()
    {
    }

    public TaskChecklistItem(Guid tenantId, Guid taskId, string label, bool isMandatory, int sortOrder)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        TaskId = taskId;
        Label = label;
        IsMandatory = isMandatory;
        SortOrder = sortOrder;
    }

    public Guid TaskId { get; private set; }
    public string Label { get; private set; } = null!;
    public bool IsDone { get; private set; }
    public bool IsMandatory { get; private set; }
    public int SortOrder { get; private set; }

    public void SetDone(bool isDone) => IsDone = isDone;
}
