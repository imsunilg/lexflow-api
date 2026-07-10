using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to crm.clients (lexflow-database Scripts/03_CRM/Clients). Module 3.
/// <see cref="PanEnc"/> is pgcrypto-encrypted at the application layer (never plaintext);
/// <see cref="DisplayName"/> is a DB-generated STORED column, read-only here.
/// </summary>
public sealed class Client : AuditableEntity
{
    private Client()
    {
    }

    public Client(
        Guid tenantId,
        string number,
        string type,
        string? firstName,
        string? lastName,
        string? legalName,
        string? email,
        string? phoneE164,
        string? gstin,
        string? cin,
        Guid? ownerId,
        Guid? branchId,
        Guid? sourceLeadId)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Number = number;
        Type = type;
        FirstName = firstName;
        LastName = lastName;
        LegalName = legalName;
        Email = email;
        PhoneE164 = phoneE164;
        Gstin = gstin;
        Cin = cin;
        Status = "Active";
        OwnerId = ownerId;
        BranchId = branchId;
        PortalEnabled = false;
        SourceLeadId = sourceLeadId;
    }

    public string Number { get; private set; } = null!;
    public string Type { get; private set; } = null!;
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string? LegalName { get; private set; }
    public string? DisplayName { get; private set; }
    public string? Email { get; private set; }
    public string? PhoneE164 { get; private set; }
    public byte[]? PanEnc { get; private set; }
    public string? Gstin { get; private set; }
    public string? Cin { get; private set; }
    public string Status { get; private set; } = "Active";
    public decimal? CreditLimit { get; private set; }
    public Guid? OwnerId { get; private set; }
    public Guid? BranchId { get; private set; }
    public bool PortalEnabled { get; private set; }
    public Guid? SourceLeadId { get; private set; }
    public Guid? MergedIntoClientId { get; private set; }

    public void Update(
        string? firstName,
        string? lastName,
        string? legalName,
        string? email,
        string? phoneE164,
        string? gstin,
        string? cin,
        decimal? creditLimit,
        Guid? ownerId,
        Guid? branchId)
    {
        FirstName = firstName;
        LastName = lastName;
        LegalName = legalName;
        Email = email;
        PhoneE164 = phoneE164;
        Gstin = gstin;
        Cin = cin;
        CreditLimit = creditLimit;
        OwnerId = ownerId;
        BranchId = branchId;
    }

    public void SetPanEnc(byte[]? panEnc) => PanEnc = panEnc;

    public void SetStatus(string status) => Status = status;

    public void SetPortalEnabled(bool enabled) => PortalEnabled = enabled;

    public void MarkMergedInto(Guid survivorId)
    {
        MergedIntoClientId = survivorId;
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
    }
}
