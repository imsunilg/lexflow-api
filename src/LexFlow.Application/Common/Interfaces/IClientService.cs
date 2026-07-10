namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 3 (Client Management) lifecycle operations — PRD §17 API list.</summary>
public interface IClientService
{
    Task<ClientDto> CreateAsync(Guid tenantId, Guid? actorId, CreateClientInput input, CancellationToken cancellationToken = default);

    Task<ClientDto?> GetByIdAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClientDto>> GetAllAsync(Guid tenantId, ClientFilter filter, CancellationToken cancellationToken = default);

    Task<ClientDto> UpdateAsync(Guid tenantId, Guid clientId, UpdateClientInput input, CancellationToken cancellationToken = default);

    /// <summary>Soft delete; blocked with 409 while any legal.matters/fin.invoices row references this client (AC-C4) — checked dynamically via to_regclass since those schemas may not exist yet in this build.</summary>
    Task DeleteAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default);

    Task<ClientContactDto> AddContactAsync(Guid tenantId, Guid clientId, ClientContactInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClientContactDto>> GetContactsAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default);

    Task<ClientContactDto> UpdateContactAsync(Guid tenantId, Guid clientId, Guid contactId, ClientContactInput input, CancellationToken cancellationToken = default);

    Task DeleteContactAsync(Guid tenantId, Guid clientId, Guid contactId, CancellationToken cancellationToken = default);

    Task<ClientAddressDto> AddAddressAsync(Guid tenantId, Guid clientId, ClientAddressInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClientAddressDto>> GetAddressesAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default);

    Task<ClientAddressDto> UpdateAddressAsync(Guid tenantId, Guid clientId, Guid addressId, ClientAddressInput input, CancellationToken cancellationToken = default);

    Task DeleteAddressAsync(Guid tenantId, Guid clientId, Guid addressId, CancellationToken cancellationToken = default);

    /// <summary>KYC upload. docNumber is encrypted via IKycEncryptionService before persistence; only the ciphertext + last-4 are ever stored (DPDP rule).</summary>
    Task<ClientIdentityDocumentDto> AddIdentityDocumentAsync(Guid tenantId, Guid clientId, string docKind, string docNumber, DateOnly? expiryDate, Guid? documentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClientIdentityDocumentDto>> GetIdentityDocumentsAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default);

    Task<ClientIdentityDocumentDto> VerifyIdentityDocumentAsync(Guid tenantId, Guid clientId, Guid documentId, Guid verifiedBy, bool approve, CancellationToken cancellationToken = default);

    Task<ClientRelationshipDto> AddRelationshipAsync(Guid tenantId, Guid clientId, Guid? relatedClientId, string? personName, string relationType, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClientRelationshipDto>> GetRelationshipsAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default);

    /// <summary>AC-C1: all tab counts in one call (P95 &lt; 1.5s target — no per-tab round-trips).</summary>
    Task<ClientSummaryDto> GetSummaryAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default);

    Task<ClientPortalUserDto> SetPortalAccessAsync(Guid tenantId, Guid clientId, bool enable, CancellationToken cancellationToken = default);

    Task ResendPortalInviteAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default);

    /// <summary>AC-C3: re-parents contacts/addresses/identity-documents/relationships (and legal.matters/fin.invoices/dms.documents rows if those schemas exist yet); tombstones the duplicate with a redirect.</summary>
    Task MergeAsync(Guid tenantId, Guid survivorId, Guid duplicateId, IReadOnlyDictionary<string, string> fieldChoices, CancellationToken cancellationToken = default);

    /// <summary>comm.* schema (Module 11) doesn't exist yet in this build — returns an empty, clearly-typed list rather than throwing, so the 360° Communication tab renders with zero rows instead of erroring.</summary>
    Task<IReadOnlyList<ClientCommunicationDto>> GetCommunicationsAsync(Guid tenantId, Guid clientId, string? channel, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken = default);
}

public sealed record CreateClientInput(
    string Type,
    string? FirstName,
    string? LastName,
    string? LegalName,
    string? Email,
    string? PhoneE164,
    string? Gstin,
    string? Cin,
    Guid? OwnerId,
    Guid? BranchId,
    Guid? SourceLeadId,
    IReadOnlyList<ClientContactInput>? Contacts);

public sealed record UpdateClientInput(
    string? FirstName,
    string? LastName,
    string? LegalName,
    string? Email,
    string? PhoneE164,
    string? Gstin,
    string? Cin,
    decimal? CreditLimit,
    Guid? OwnerId,
    Guid? BranchId);

public sealed record ClientFilter(string? Type, string? Status, Guid? OwnerId, Guid? BranchId, string? Query);

public sealed record ClientContactInput(string Name, string? Designation, string? Email, string? Phone, bool IsPrimary);

public sealed record ClientAddressInput(string Kind, string Line1, string? Line2, string? City, string? StateCode, string? Postal, string? Country, bool IsPrimaryOfKind);

public sealed record ClientContactDto(Guid Id, Guid ClientId, string Name, string? Designation, string? Email, string? Phone, bool IsPrimary);

public sealed record ClientAddressDto(Guid Id, Guid ClientId, string Kind, string Line1, string? Line2, string? City, string? StateCode, string? Postal, string? Country, bool IsPrimaryOfKind);

public sealed record ClientIdentityDocumentDto(Guid Id, Guid ClientId, string DocKind, string Last4, DateOnly? ExpiryDate, string VerifyStatus, Guid? VerifiedBy, DateTimeOffset? VerifiedAt);

public sealed record ClientRelationshipDto(Guid Id, Guid ClientId, Guid? RelatedClientId, string? PersonName, string RelationType);

public sealed record ClientCommunicationDto(Guid Id, string Channel, DateTimeOffset At, string? Subject, string? Snippet);

public sealed record ClientSummaryDto(
    int OpenMatters,
    decimal LifetimeBilled,
    decimal Outstanding,
    decimal TrustBalance,
    DateTimeOffset? NextHearing,
    int DocumentCount,
    int InvoiceCount);

public sealed record ClientPortalUserDto(Guid Id, Guid ClientId, string Email, string Status, bool TwoFaEnabled);

public sealed record ClientDto(
    Guid Id,
    string Number,
    string Type,
    string? FirstName,
    string? LastName,
    string? LegalName,
    string? DisplayName,
    string? Email,
    string? PhoneE164,
    string? Gstin,
    string? Cin,
    string Status,
    decimal? CreditLimit,
    Guid? OwnerId,
    Guid? BranchId,
    bool PortalEnabled,
    Guid? SourceLeadId,
    Guid? MergedIntoClientId,
    DateTimeOffset CreatedAt);
