namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 17: "my matters cards" home list + per-matter sanitized timeline. BR-10: "Nothing
/// reaches the portal without explicit publish flag (documents) or granularity policy
/// (timeline); internal notes and strategy classifications can never be published (hard
/// filter by type)" — GetTimelineAsync only ever projects hearings/hearing_outcomes rows
/// whose PortalVisible flag is true, never raw case-file content.
/// </summary>
public interface IPortalTimelineService
{
    /// <summary>Every matter belonging to clientId, filtered further by the caller's ClientPortalUser.VisibleMatterIds subset (corporate per-user scoping).</summary>
    Task<IReadOnlyList<PortalMatterSummaryDto>> GetMyMattersAsync(Guid tenantId, Guid clientId, IReadOnlyCollection<Guid>? visibleMatterIds, CancellationToken cancellationToken = default);

    /// <summary>Throws NotFoundException if matterId does not belong to clientId (or is outside the caller's visible-matter subset) — the IDOR gate for this endpoint.</summary>
    Task<PortalMatterTimelineDto> GetTimelineAsync(Guid tenantId, Guid clientId, IReadOnlyCollection<Guid>? visibleMatterIds, Guid matterId, CancellationToken cancellationToken = default);
}

public sealed record PortalMatterSummaryDto(Guid Id, string Number, string Title, string Status, DateOnly? NextHearingDate, string? ResponsibleLawyerName);

public sealed record PortalTimelineEntryDto(string Kind, DateTimeOffset At, string Title, string? Detail);

public sealed record PortalMatterTimelineDto(Guid MatterId, string Number, string Title, string Status, IReadOnlyList<PortalTimelineEntryDto> Entries);
