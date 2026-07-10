using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to legal.argument_notes (lexflow-database Scripts/04_Legal/ArgumentNotes).</summary>
public sealed class ArgumentNote : AuditableEntity
{
    private ArgumentNote()
    {
    }

    public ArgumentNote(Guid tenantId, Guid caseId, Guid? hearingId, string? stage, string body, Guid[]? citationJudgmentIds)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        CaseId = caseId;
        HearingId = hearingId;
        Stage = stage;
        Body = body;
        CitationJudgmentIds = citationJudgmentIds;
    }

    public Guid CaseId { get; private set; }
    public Guid? HearingId { get; private set; }
    public string? Stage { get; private set; }
    public string Body { get; private set; } = null!;
    public Guid[]? CitationJudgmentIds { get; private set; }
}
