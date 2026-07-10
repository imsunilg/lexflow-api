using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to dms.signature_envelopes (lexflow-database Scripts/05_DMS/SignatureEnvelopes). Module 7: DocuSign/AdobeSign e-signature.</summary>
public sealed class SignatureEnvelope : AuditableEntity
{
    private SignatureEnvelope()
    {
    }

    public SignatureEnvelope(Guid tenantId, Guid documentId, string provider)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        DocumentId = documentId;
        Provider = provider;
        Status = "Draft";
    }

    public Guid DocumentId { get; private set; }
    public string Provider { get; private set; } = null!;
    public string? ProviderEnvelopeId { get; private set; }
    public string Status { get; private set; } = "Draft";
    public Guid? CompletedDocVersionId { get; private set; }

    public void SetProviderEnvelopeId(string providerEnvelopeId)
    {
        ProviderEnvelopeId = providerEnvelopeId;
        Status = "Sent";
    }

    public void SetStatus(string status) => Status = status;

    public void Complete(Guid completedDocVersionId)
    {
        Status = "Completed";
        CompletedDocVersionId = completedDocVersionId;
    }
}
