using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to kb.kb_collections (lexflow-database Scripts/09_KB/KbCollections). Module 12: "collections (curated sets e.g. 'Cheque bounce defense pack')".</summary>
public sealed class KbCollection : AuditableEntity
{
    private KbCollection()
    {
    }

    public KbCollection(Guid tenantId, string name, string? description)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Name = name;
        Description = description;
    }

    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }

    public void Update(string name, string? description) => (Name, Description) = (name, description);
}
