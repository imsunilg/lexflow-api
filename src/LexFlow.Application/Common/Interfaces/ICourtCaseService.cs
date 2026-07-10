namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 5 (Court Case Management) case-level operations — PRD §17 API list.</summary>
public interface ICourtCaseService
{
    Task<CourtCaseDto> CreateAsync(Guid tenantId, Guid matterId, CreateCourtCaseInput input, CancellationToken cancellationToken = default);

    Task<CourtCaseDto?> GetByIdAsync(Guid tenantId, Guid caseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CourtCaseDto>> GetByMatterAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default);

    Task<CourtCaseDto> UpdateAsync(Guid tenantId, Guid caseId, UpdateCourtCaseInput input, CancellationToken cancellationToken = default);

    Task<CourtCaseDto> ChangeStageAsync(Guid tenantId, Guid? actorId, Guid caseId, string toStage, CancellationToken cancellationToken = default);

    /// <summary>AC-CC4: clones case core into a new Court Case linked Appeal-of in a higher court; carries forward selected documents by reference (no blob duplication).</summary>
    Task<CourtCaseDto> FileAppealAsync(Guid tenantId, Guid? actorId, Guid caseId, Guid targetCourtId, IReadOnlyList<Guid> carryDocumentIds, CancellationToken cancellationToken = default);

    Task<CasePartyDto> AddPartyAsync(Guid tenantId, Guid caseId, CasePartyInput input, CancellationToken cancellationToken = default);

    Task<CasePartyDto> UpdatePartyAsync(Guid tenantId, Guid caseId, Guid partyId, CasePartyInput input, CancellationToken cancellationToken = default);

    Task DeletePartyAsync(Guid tenantId, Guid caseId, Guid partyId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CasePartyDto>> GetPartiesAsync(Guid tenantId, Guid caseId, CancellationToken cancellationToken = default);
}

public sealed record CreateCourtCaseInput(Guid CourtId, string CaseType, string CaseNumber, int CaseYear, string? CnrNumber, DateOnly? FilingDate, string? Stage, Guid? JudgeId, string? Courtroom);

public sealed record UpdateCourtCaseInput(string? CnrNumber, DateOnly? FilingDate, Guid? JudgeId, string? Courtroom);

public sealed record CasePartyInput(string PartyRole, string Name, string? AdvocateName, Guid? AdvocateUserId, string ContactJson);

public sealed record CasePartyDto(Guid Id, Guid CaseId, string PartyRole, string Name, string? AdvocateName, Guid? AdvocateUserId, string ContactJson);

public sealed record CourtCaseDto(
    Guid Id,
    Guid MatterId,
    Guid CourtId,
    string CaseType,
    string CaseNumber,
    int CaseYear,
    string? CnrNumber,
    DateOnly? FilingDate,
    string? Stage,
    Guid? JudgeId,
    string? Courtroom,
    string Status,
    Guid? AppealOfCaseId,
    DateTimeOffset CreatedAt);
