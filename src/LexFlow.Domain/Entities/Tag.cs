using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to dms.tags (lexflow-database Scripts/05_DMS/Tags).</summary>
public sealed class Tag : AuditableEntity
{
    private Tag()
    {
    }

    public Tag(Guid tenantId, string name)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Name = name;
    }

    public string Name { get; private set; } = null!;
}
