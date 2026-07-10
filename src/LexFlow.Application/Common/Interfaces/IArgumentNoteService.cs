namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 5 arguments notes — PRD §17 (POST /cases/{id}/arguments).</summary>
public interface IArgumentNoteService
{
    Task<ArgumentNoteDto> AddAsync(Guid tenantId, Guid caseId, Guid? hearingId, string? stage, string body, IReadOnlyList<Guid>? citationJudgmentIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ArgumentNoteDto>> GetByCaseAsync(Guid tenantId, Guid caseId, CancellationToken cancellationToken = default);
}

public sealed record ArgumentNoteDto(Guid Id, Guid CaseId, Guid? HearingId, string? Stage, string Body, IReadOnlyList<Guid>? CitationJudgmentIds);
