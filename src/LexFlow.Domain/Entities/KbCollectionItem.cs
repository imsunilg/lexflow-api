using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to kb.kb_collection_items (lexflow-database Scripts/09_KB/KbCollectionItems).</summary>
public sealed class KbCollectionItem : AuditableEntity
{
    private KbCollectionItem()
    {
    }

    public KbCollectionItem(Guid tenantId, Guid collectionId, string kbRefKind, Guid kbRefId, int sortOrder)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        CollectionId = collectionId;
        KbRefKind = kbRefKind;
        KbRefId = kbRefId;
        SortOrder = sortOrder;
    }

    public Guid CollectionId { get; private set; }
    public string KbRefKind { get; private set; } = null!;
    public Guid KbRefId { get; private set; }
    public int SortOrder { get; private set; }

    public void Reorder(int sortOrder) => SortOrder = sortOrder;
}
