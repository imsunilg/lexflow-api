using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to kb.kb_bookmarks (lexflow-database Scripts/09_KB/KbBookmarks). Module 12: "bookmarks (personal)".</summary>
public sealed class KbBookmark : AuditableEntity
{
    private KbBookmark()
    {
    }

    public KbBookmark(Guid tenantId, Guid userId, string kbRefKind, Guid kbRefId)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        UserId = userId;
        KbRefKind = kbRefKind;
        KbRefId = kbRefId;
    }

    public Guid UserId { get; private set; }
    public string KbRefKind { get; private set; } = null!;
    public Guid KbRefId { get; private set; }
}
