using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to ops.task_templates (lexflow-database Scripts/07_Ops/TaskTemplates).</summary>
public sealed class TaskTemplate : AuditableEntity
{
    private TaskTemplate()
    {
    }

    public TaskTemplate(Guid tenantId, string name, string? matterType)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Name = name;
        MatterType = matterType;
        IsActive = true;
    }

    public string Name { get; private set; } = null!;
    public string? MatterType { get; private set; }
    public bool IsActive { get; private set; }

    public void Update(string name, string? matterType, bool isActive)
    {
        Name = name;
        MatterType = matterType;
        IsActive = isActive;
    }
}
