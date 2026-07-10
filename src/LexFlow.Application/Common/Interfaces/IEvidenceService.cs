namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 5 evidence register — PRD §17. AC-CC5: custody log is append-only (DB trigger backstop), edits append, never overwrite.</summary>
public interface IEvidenceService
{
    Task<EvidenceItemDto> AddAsync(Guid tenantId, Guid caseId, string? exhibitNo, string kind, string? description, Guid? documentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EvidenceItemDto>> GetByCaseAsync(Guid tenantId, Guid caseId, CancellationToken cancellationToken = default);

    Task<EvidenceCustodyLogDto> AddCustodyEventAsync(Guid tenantId, Guid evidenceId, string action, string? holder, string? note, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EvidenceCustodyLogDto>> GetCustodyChainAsync(Guid tenantId, Guid evidenceId, CancellationToken cancellationToken = default);
}

public sealed record EvidenceItemDto(Guid Id, Guid CaseId, string? ExhibitNo, string Kind, string? Description, bool Marked, bool Objected, string? CustodyStatus, Guid? DocumentId);

public sealed record EvidenceCustodyLogDto(Guid Id, Guid EvidenceId, string Action, string? Holder, DateTimeOffset At, string? Note);
