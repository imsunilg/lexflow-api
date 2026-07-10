using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to ops.task_template_items (lexflow-database
/// Scripts/07_Ops/TaskTemplateItems). relative_due_days is counted from the matter's
/// open/filing date at apply-time (AC-TK3).
/// </summary>
public sealed class TaskTemplateItem : AuditableEntity
{
    private TaskTemplateItem()
    {
    }

    public TaskTemplateItem(Guid tenantId, Guid templateId, string title, int relativeDueDays, string? category, int sortOrder, bool isMandatory)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        TemplateId = templateId;
        Title = title;
        RelativeDueDays = relativeDueDays;
        Category = category;
        SortOrder = sortOrder;
        IsMandatory = isMandatory;
    }

    public Guid TemplateId { get; private set; }
    public string Title { get; private set; } = null!;
    public int RelativeDueDays { get; private set; }
    public string? Category { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsMandatory { get; private set; }
}
