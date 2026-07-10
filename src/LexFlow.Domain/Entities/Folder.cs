using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to dms.folders (lexflow-database Scripts/05_DMS/Folders). Module 7 — depth ≤ 10 enforced at the application layer.</summary>
public sealed class Folder : AuditableEntity
{
    private Folder()
    {
    }

    public Folder(Guid tenantId, string name, Guid? parentId, Guid? matterId, string? path)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Name = name;
        ParentId = parentId;
        MatterId = matterId;
        Path = path;
    }

    public string Name { get; private set; } = null!;
    public Guid? ParentId { get; private set; }
    public Guid? MatterId { get; private set; }
    public string? Path { get; private set; }

    public void Rename(string name) => Name = name;

    public void MoveTo(Guid? newParentId, string? newPath)
    {
        ParentId = newParentId;
        Path = newPath;
    }
}
