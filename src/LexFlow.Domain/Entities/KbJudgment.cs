using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to kb.kb_judgments (lexflow-database Scripts/09_KB/KbJudgments). Module 12: "Judgments/Case Laws (citation, court, bench, date, parties, headnote, full text PDF, tags)".</summary>
public sealed class KbJudgment : AuditableEntity
{
    private KbJudgment()
    {
    }

    public KbJudgment(Guid tenantId, string citation, string? neutralCitation, Guid? courtId, DateOnly? decisionDate, string? parties, string? headnote, Guid? documentId)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Citation = citation;
        NeutralCitation = neutralCitation;
        CourtId = courtId;
        DecisionDate = decisionDate;
        Parties = parties;
        Headnote = headnote;
        DocumentId = documentId;
    }

    public string Citation { get; private set; } = null!;
    public string? NeutralCitation { get; private set; }
    public Guid? CourtId { get; private set; }
    public DateOnly? DecisionDate { get; private set; }
    public string? Parties { get; private set; }
    public string? Headnote { get; private set; }
    public Guid? DocumentId { get; private set; }

    /// <summary>Module 12 User Flow #5: "judgment headnotes editable with history" — the history side is the core.audit_events trail (§30's generic audit interceptor), not a dedicated versions table.</summary>
    public void UpdateHeadnote(string? headnote) => Headnote = headnote;

    public void UpdateMetadata(string? neutralCitation, Guid? courtId, DateOnly? decisionDate, string? parties)
    {
        NeutralCitation = neutralCitation;
        CourtId = courtId;
        DecisionDate = decisionDate;
        Parties = parties;
    }

    public void AttachDocument(Guid documentId) => DocumentId = documentId;
}
