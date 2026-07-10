using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to legal.matter_related (lexflow-database Scripts/04_Legal/MatterRelated). Self-referencing, typed.</summary>
public sealed class MatterRelated : AuditableEntity
{
    private MatterRelated()
    {
    }

    public MatterRelated(Guid tenantId, Guid matterId, Guid relatedMatterId, string relationType)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        MatterId = matterId;
        RelatedMatterId = relatedMatterId;
        RelationType = relationType;
    }

    public Guid MatterId { get; private set; }
    public Guid RelatedMatterId { get; private set; }
    public string RelationType { get; private set; } = null!;
}
