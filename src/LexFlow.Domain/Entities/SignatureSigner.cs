using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to dms.signature_signers (lexflow-database Scripts/05_DMS/SignatureSigners).</summary>
public sealed class SignatureSigner : AuditableEntity
{
    private SignatureSigner()
    {
    }

    public SignatureSigner(Guid tenantId, Guid envelopeId, string name, string email, int orderNo)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        EnvelopeId = envelopeId;
        Name = name;
        Email = email;
        OrderNo = orderNo;
        Status = "Pending";
    }

    public Guid EnvelopeId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public int OrderNo { get; private set; }
    public string Status { get; private set; } = "Pending";
    public DateTimeOffset? SignedAt { get; private set; }

    public void SetStatus(string status)
    {
        Status = status;
        if (status == "Signed")
        {
            SignedAt = DateTimeOffset.UtcNow;
        }
    }
}
