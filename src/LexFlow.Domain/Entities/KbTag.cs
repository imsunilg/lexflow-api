using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to kb.kb_tags (lexflow-database Scripts/09_KB/KbTags). Module 12: "taxonomy tags (practice area, legal issue)".</summary>
public sealed class KbTag : AuditableEntity
{
    private KbTag()
    {
    }

    public KbTag(Guid tenantId, string name)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Name = name;
    }

    public string Name { get; private set; } = null!;
}
