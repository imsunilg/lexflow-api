using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to kb.kb_item_tags (lexflow-database Scripts/09_KB/KbItemTags). Polymorphic tag attachment across Act/ActSection/Judgment/Article/Template.</summary>
public sealed class KbItemTag : AuditableEntity
{
    private KbItemTag()
    {
    }

    public KbItemTag(Guid tenantId, Guid tagId, string kbRefKind, Guid kbRefId)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        TagId = tagId;
        KbRefKind = kbRefKind;
        KbRefId = kbRefId;
    }

    public Guid TagId { get; private set; }
    public string KbRefKind { get; private set; } = null!;
    public Guid KbRefId { get; private set; }
}
