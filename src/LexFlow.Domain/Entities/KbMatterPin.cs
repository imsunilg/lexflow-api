using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to kb.kb_matter_pins (lexflow-database Scripts/09_KB/KbMatterPins).
/// Module 12 edge case: "orphan pins after KB item unpublish (pin retains snapshot text)" —
/// <see cref="SnapshotText"/> freezes the cited content at pin time (AC-KB4), so the pin never
/// needs to re-read the source KB item to render.
/// </summary>
public sealed class KbMatterPin : AuditableEntity
{
    private KbMatterPin()
    {
    }

    public KbMatterPin(Guid tenantId, Guid matterId, string kbRefKind, Guid kbRefId, string? note, string? snapshotText, Guid? pinnedBy)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        MatterId = matterId;
        KbRefKind = kbRefKind;
        KbRefId = kbRefId;
        Note = note;
        SnapshotText = snapshotText;
        PinnedBy = pinnedBy;
        PinnedAt = DateTimeOffset.UtcNow;
    }

    public Guid MatterId { get; private set; }
    public string KbRefKind { get; private set; } = null!;
    public Guid KbRefId { get; private set; }
    public string? Note { get; private set; }
    public string? SnapshotText { get; private set; }
    public Guid? PinnedBy { get; private set; }
    public DateTimeOffset PinnedAt { get; private set; }

    public void UpdateNote(string? note) => Note = note;
}
