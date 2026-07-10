namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 12: Judgments/Case Laws. Upload pipeline mirrors Module 7's (AV scan -&gt; blob store -&gt;
/// dms.documents/document_versions row) but is owned here rather than delegated to IDocumentService,
/// since judgments aren't filed in folders and have their own metadata shape. Error Handling:
/// "OCR fail on judgment -&gt; metadata-only searchable + retry" — <see cref="RetryExtractionAsync"/>
/// is that retry.
/// </summary>
public interface IKbJudgmentService
{
    /// <summary>Validation Rules: "judgment PDF ≤ 50 MB". Edge case: "duplicate judgment upload (citation match prompt merge)" — a citation that already exists for this tenant throws ConflictException("DUPLICATE_CITATION", ...) carrying the existing judgment's id rather than silently creating a second row.</summary>
    Task<KbJudgmentDto> UploadAsync(Guid tenantId, Guid? actorId, UploadJudgmentInput input, byte[] fileContent, string fileName, string? mime, CancellationToken cancellationToken = default);

    Task<KbJudgmentDto?> GetAsync(Guid tenantId, Guid judgmentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KbJudgmentDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<KbJudgmentDto> UpdateAsync(Guid tenantId, Guid judgmentId, UpdateJudgmentInput input, CancellationToken cancellationToken = default);

    /// <summary>Module 12 User Flow #5: "judgment headnotes editable with history."</summary>
    Task<KbJudgmentDto> UpdateHeadnoteAsync(Guid tenantId, Guid judgmentId, string? headnote, CancellationToken cancellationToken = default);

    Task RetryExtractionAsync(Guid tenantId, Guid judgmentId, CancellationToken cancellationToken = default);

    /// <summary>AC-KB4's "back-link: pinned in N matters" counter.</summary>
    Task<int> GetPinCountAsync(Guid tenantId, Guid judgmentId, CancellationToken cancellationToken = default);
}

public sealed record UploadJudgmentInput(string Citation, string? NeutralCitation, Guid? CourtId, DateOnly? DecisionDate, string? Parties, string? Headnote);

public sealed record UpdateJudgmentInput(string? NeutralCitation, Guid? CourtId, DateOnly? DecisionDate, string? Parties);

public sealed record KbJudgmentDto(Guid Id, string Citation, string? NeutralCitation, Guid? CourtId, DateOnly? DecisionDate, string? Parties, string? Headnote, Guid? DocumentId, string OcrStatus);
