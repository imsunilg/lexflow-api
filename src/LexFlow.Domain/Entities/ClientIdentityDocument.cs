using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to crm.client_identity_documents (lexflow-database
/// Scripts/03_CRM/ClientIdentityDocuments). KYC docs — <see cref="DocNumberEnc"/> is
/// pgcrypto-encrypted (pgp_sym_encrypt, via <c>IKycEncryptionService</c>); the plaintext
/// document number is NEVER persisted anywhere, only this ciphertext plus <see cref="Last4"/>.
/// </summary>
public sealed class ClientIdentityDocument : AuditableEntity
{
    private ClientIdentityDocument()
    {
    }

    public ClientIdentityDocument(Guid tenantId, Guid clientId, string docKind, byte[] docNumberEnc, string last4, DateOnly? expiryDate, Guid? documentId)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ClientId = clientId;
        DocKind = docKind;
        DocNumberEnc = docNumberEnc;
        Last4 = last4;
        ExpiryDate = expiryDate;
        DocumentId = documentId;
        VerifyStatus = "Pending";
    }

    public Guid ClientId { get; private set; }
    public string DocKind { get; private set; } = null!;
    public byte[] DocNumberEnc { get; private set; } = null!;
    public string Last4 { get; private set; } = null!;
    public DateOnly? ExpiryDate { get; private set; }
    public Guid? DocumentId { get; private set; }
    public string VerifyStatus { get; private set; } = "Pending";
    public Guid? VerifiedBy { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }

    public void Verify(Guid verifiedBy)
    {
        VerifyStatus = "Verified";
        VerifiedBy = verifiedBy;
        VerifiedAt = DateTimeOffset.UtcNow;
    }

    public void Reject(Guid verifiedBy)
    {
        VerifyStatus = "Rejected";
        VerifiedBy = verifiedBy;
        VerifiedAt = DateTimeOffset.UtcNow;
    }

    public void Reparent(Guid newClientId) => ClientId = newClientId;
}
