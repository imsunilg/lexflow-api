using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to dms.documents (lexflow-database Scripts/05_DMS/Documents). Module 7. AC-DOC3: confidentiality gate must be checked in every query.</summary>
public sealed class Document : AuditableEntity
{
    private Document()
    {
    }

    public Document(Guid tenantId, Guid? folderId, Guid? matterId, Guid? clientId, Guid? caseId, string title, string docType, string confidentiality)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        FolderId = folderId;
        MatterId = matterId;
        ClientId = clientId;
        CaseId = caseId;
        Title = title;
        DocType = docType;
        Confidentiality = confidentiality;
        PortalPublished = false;
    }

    public Guid? FolderId { get; private set; }
    public Guid? MatterId { get; private set; }
    public Guid? ClientId { get; private set; }
    public Guid? CaseId { get; private set; }
    public string Title { get; private set; } = null!;
    public string DocType { get; private set; } = null!;
    public string Confidentiality { get; private set; } = "Normal";
    public Guid? CurrentVersionId { get; private set; }
    public bool PortalPublished { get; private set; }

    public void UpdateMetadata(string title, string docType, string confidentiality, Guid? folderId)
    {
        Title = title;
        DocType = docType;
        Confidentiality = confidentiality;
        FolderId = folderId;
    }

    public void SetPortalPublished(bool published) => PortalPublished = published;
}
