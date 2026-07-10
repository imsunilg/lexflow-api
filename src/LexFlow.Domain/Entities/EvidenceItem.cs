using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to legal.evidence_items (lexflow-database Scripts/04_Legal/EvidenceItems).</summary>
public sealed class EvidenceItem : AuditableEntity
{
    private EvidenceItem()
    {
    }

    public EvidenceItem(Guid tenantId, Guid caseId, string? exhibitNo, string kind, string? description, Guid? documentId)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        CaseId = caseId;
        ExhibitNo = exhibitNo;
        Kind = kind;
        Description = description;
        DocumentId = documentId;
    }

    public Guid CaseId { get; private set; }
    public string? ExhibitNo { get; private set; }
    public string Kind { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool Marked { get; private set; }
    public bool Objected { get; private set; }
    public string? CustodyStatus { get; private set; }
    public Guid? DocumentId { get; private set; }

    public void SetMarked(bool marked) => Marked = marked;

    public void SetObjected(bool objected) => Objected = objected;

    public void SetCustodyStatus(string status) => CustodyStatus = status;
}
