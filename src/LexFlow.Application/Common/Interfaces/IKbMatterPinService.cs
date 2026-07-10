namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 12: pin-to-matter (cite a KB item into a matter's Arguments/Research tab with a pin
/// note). AC-KB4/edge case: "orphan pins after KB item unpublish (pin retains snapshot text)" —
/// <see cref="PinAsync"/> resolves and freezes the source item's current text into snapshot_text
/// at pin time, so a pin is never affected by the source item later being edited, unpublished, or
/// even deleted.
/// </summary>
public interface IKbMatterPinService
{
    Task<KbMatterPinDto> PinAsync(Guid tenantId, Guid matterId, Guid? pinnedBy, string kbRefKind, Guid kbRefId, string? note, CancellationToken cancellationToken = default);

    Task UnpinAsync(Guid tenantId, Guid pinId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KbMatterPinDto>> GetForMatterAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default);

    /// <summary>AC-KB4: "back-link 'pinned in 3 matters'".</summary>
    Task<int> GetPinCountAsync(Guid tenantId, string kbRefKind, Guid kbRefId, CancellationToken cancellationToken = default);
}

public sealed record KbMatterPinDto(Guid Id, Guid MatterId, string KbRefKind, Guid KbRefId, string? Note, string? SnapshotText, Guid? PinnedBy, DateTimeOffset PinnedAt);
