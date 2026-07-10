using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to legal.practice_areas (lexflow-database Scripts/04_Legal/PracticeAreas). Configurable tree, self-referencing.</summary>
public sealed class PracticeArea : AuditableEntity
{
    private PracticeArea()
    {
    }

    public PracticeArea(Guid tenantId, string name, Guid? parentId)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Name = name;
        ParentId = parentId;
        IsActive = true;
    }

    public string Name { get; private set; } = null!;
    public Guid? ParentId { get; private set; }
    public bool IsActive { get; private set; }

    public void Rename(string name) => Name = name;

    public void SetActive(bool isActive) => IsActive = isActive;
}
