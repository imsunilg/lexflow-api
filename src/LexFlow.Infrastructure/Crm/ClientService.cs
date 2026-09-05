using System.Data.Common;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Crm;

/// <summary>Module 3 (Client Management) — PRD §17 API list.</summary>
public sealed class ClientService(LexFlowDbContext db, IKycEncryptionService kycEncryption, IWorkflowEventPublisher? workflowEvents = null) : IClientService
{
    public async Task<ClientDto> CreateAsync(Guid tenantId, Guid? actorId, CreateClientInput input, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(input.PhoneE164) || !string.IsNullOrWhiteSpace(input.Email))
        {
            var duplicate = await db.Clients.SingleOrDefaultAsync(
                c => c.TenantId == tenantId && c.Type == input.Type &&
                     ((input.PhoneE164 != null && c.PhoneE164 == input.PhoneE164) || (input.Email != null && c.Email == input.Email)),
                cancellationToken);
            if (duplicate is not null)
            {
                throw new ConflictException($"A client with this phone/email already exists (id: {duplicate.Id}).", "CLIENT_DUPLICATE");
            }
        }

        var number = await GenerateNumberAsync(tenantId, cancellationToken);
        var client = new Client(
            tenantId,
            number,
            input.Type,
            input.FirstName,
            input.LastName,
            input.LegalName,
            input.Email,
            input.PhoneE164,
            input.Gstin,
            input.Cin,
            input.OwnerId,
            input.BranchId,
            input.SourceLeadId);

        await db.Clients.AddAsync(client, cancellationToken);

        if (input.Contacts is { Count: > 0 })
        {
            foreach (var contact in input.Contacts)
            {
                await db.ClientContacts.AddAsync(new ClientContact(tenantId, client.Id, contact.Name, contact.Designation, contact.Email, contact.Phone, contact.IsPrimary), cancellationToken);
            }
        }

        if (workflowEvents is not null)
        {
            await workflowEvents.PublishAsync(tenantId, "client.created", client.Id, new
            {
                entityId = client.Id,
                ownerId = client.OwnerId,
                branchId = client.BranchId,
            }, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(client);
    }

    public async Task<ClientDto?> GetByIdAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default)
    {
        var client = await db.Clients.SingleOrDefaultAsync(c => c.TenantId == tenantId && c.Id == clientId, cancellationToken);
        if (client is not null)
        {
            return ToDto(client);
        }

        // AC-C3: "old id URL redirects" — a merged (tombstoned) client is soft-deleted, so
        // the default query filter hides it; look it up explicitly to surface the redirect.
        var merged = await db.Clients.IgnoreQueryFilters()
            .SingleOrDefaultAsync(c => c.TenantId == tenantId && c.Id == clientId && c.MergedIntoClientId != null, cancellationToken);
        return merged is null ? null : ToDto(merged);
    }

    public async Task<IReadOnlyList<ClientDto>> GetAllAsync(Guid tenantId, ClientFilter filter, CancellationToken cancellationToken = default)
    {
        var query = db.Clients.Where(c => c.TenantId == tenantId).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Type))
        {
            query = query.Where(c => c.Type == filter.Type);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(c => c.Status == filter.Status);
        }

        if (filter.OwnerId.HasValue)
        {
            query = query.Where(c => c.OwnerId == filter.OwnerId);
        }

        if (filter.BranchId.HasValue)
        {
            query = query.Where(c => c.BranchId == filter.BranchId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var q = $"%{filter.Query}%";
            query = query.Where(c =>
                (c.FirstName != null && EF.Functions.ILike(c.FirstName, q)) ||
                (c.LastName != null && EF.Functions.ILike(c.LastName, q)) ||
                (c.LegalName != null && EF.Functions.ILike(c.LegalName, q)) ||
                (c.Email != null && EF.Functions.ILike(c.Email, q)) ||
                (c.PhoneE164 != null && EF.Functions.ILike(c.PhoneE164, q)));
        }

        var clients = await query.OrderByDescending(c => c.CreatedAt).ToListAsync(cancellationToken);
        return clients.Select(ToDto).ToList();
    }

    public async Task<ClientDto> UpdateAsync(Guid tenantId, Guid clientId, UpdateClientInput input, CancellationToken cancellationToken = default)
    {
        var client = await GetOrThrowAsync(tenantId, clientId, cancellationToken);
        client.Update(input.FirstName, input.LastName, input.LegalName, input.Email, input.PhoneE164, input.Gstin, input.Cin, input.CreditLimit, input.OwnerId, input.BranchId);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(client);
    }

    public async Task DeleteAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default)
    {
        var client = await GetOrThrowAsync(tenantId, clientId, cancellationToken);

        var matterCount = await CountReferencingRowsAsync("legal.matters", "client_id", clientId, cancellationToken);
        if (matterCount > 0)
        {
            throw new ConflictException($"Cannot delete client — {matterCount} matter(s) still reference it.", "CLIENT_HAS_OPEN_MATTERS");
        }

        var unpaidInvoiceCount = await CountReferencingRowsAsync("fin.invoices", "client_id", clientId, cancellationToken, "status <> 'Paid' AND ");
        if (unpaidInvoiceCount > 0)
        {
            throw new ConflictException($"Cannot delete client — {unpaidInvoiceCount} unpaid invoice(s) still reference it.", "CLIENT_HAS_UNPAID_INVOICES");
        }

        db.Clients.Remove(client);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ClientContactDto> AddContactAsync(Guid tenantId, Guid clientId, ClientContactInput input, CancellationToken cancellationToken = default)
    {
        await GetOrThrowAsync(tenantId, clientId, cancellationToken);
        var contact = new ClientContact(tenantId, clientId, input.Name, input.Designation, input.Email, input.Phone, input.IsPrimary);
        await db.ClientContacts.AddAsync(contact, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToContactDto(contact);
    }

    public async Task<IReadOnlyList<ClientContactDto>> GetContactsAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default)
    {
        var contacts = await db.ClientContacts.Where(c => c.TenantId == tenantId && c.ClientId == clientId).ToListAsync(cancellationToken);
        return contacts.Select(ToContactDto).ToList();
    }

    public async Task<ClientContactDto> UpdateContactAsync(Guid tenantId, Guid clientId, Guid contactId, ClientContactInput input, CancellationToken cancellationToken = default)
    {
        var contact = await db.ClientContacts.SingleOrDefaultAsync(c => c.TenantId == tenantId && c.ClientId == clientId && c.Id == contactId, cancellationToken)
            ?? throw new NotFoundException(nameof(ClientContact), contactId);

        contact.Update(input.Name, input.Designation, input.Email, input.Phone, input.IsPrimary);
        await db.SaveChangesAsync(cancellationToken);
        return ToContactDto(contact);
    }

    public async Task DeleteContactAsync(Guid tenantId, Guid clientId, Guid contactId, CancellationToken cancellationToken = default)
    {
        var contact = await db.ClientContacts.SingleOrDefaultAsync(c => c.TenantId == tenantId && c.ClientId == clientId && c.Id == contactId, cancellationToken)
            ?? throw new NotFoundException(nameof(ClientContact), contactId);

        db.ClientContacts.Remove(contact);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ClientAddressDto> AddAddressAsync(Guid tenantId, Guid clientId, ClientAddressInput input, CancellationToken cancellationToken = default)
    {
        await GetOrThrowAsync(tenantId, clientId, cancellationToken);

        if (input.IsPrimaryOfKind)
        {
            await ClearExistingPrimaryOfKindAsync(tenantId, clientId, input.Kind, cancellationToken);
        }

        var address = new ClientAddress(tenantId, clientId, input.Kind, input.Line1, input.Line2, input.City, input.StateCode, input.Postal, input.Country, input.IsPrimaryOfKind);
        await db.ClientAddresses.AddAsync(address, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToAddressDto(address);
    }

    public async Task<IReadOnlyList<ClientAddressDto>> GetAddressesAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default)
    {
        var addresses = await db.ClientAddresses.Where(a => a.TenantId == tenantId && a.ClientId == clientId).ToListAsync(cancellationToken);
        return addresses.Select(ToAddressDto).ToList();
    }

    public async Task<ClientAddressDto> UpdateAddressAsync(Guid tenantId, Guid clientId, Guid addressId, ClientAddressInput input, CancellationToken cancellationToken = default)
    {
        var address = await db.ClientAddresses.SingleOrDefaultAsync(a => a.TenantId == tenantId && a.ClientId == clientId && a.Id == addressId, cancellationToken)
            ?? throw new NotFoundException(nameof(ClientAddress), addressId);

        if (input.IsPrimaryOfKind)
        {
            await ClearExistingPrimaryOfKindAsync(tenantId, clientId, input.Kind, cancellationToken, excludeId: addressId);
        }

        address.Update(input.Kind, input.Line1, input.Line2, input.City, input.StateCode, input.Postal, input.Country, input.IsPrimaryOfKind);
        await db.SaveChangesAsync(cancellationToken);
        return ToAddressDto(address);
    }

    public async Task DeleteAddressAsync(Guid tenantId, Guid clientId, Guid addressId, CancellationToken cancellationToken = default)
    {
        var address = await db.ClientAddresses.SingleOrDefaultAsync(a => a.TenantId == tenantId && a.ClientId == clientId && a.Id == addressId, cancellationToken)
            ?? throw new NotFoundException(nameof(ClientAddress), addressId);

        db.ClientAddresses.Remove(address);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ClientIdentityDocumentDto> AddIdentityDocumentAsync(Guid tenantId, Guid clientId, string docKind, string docNumber, DateOnly? expiryDate, Guid? documentId, CancellationToken cancellationToken = default)
    {
        await GetOrThrowAsync(tenantId, clientId, cancellationToken);

        var last4 = docNumber.Length >= 4 ? docNumber[^4..] : docNumber.PadLeft(4, '0');
        var encrypted = await kycEncryption.EncryptAsync(docNumber, cancellationToken);

        var document = new ClientIdentityDocument(tenantId, clientId, docKind, encrypted, last4, expiryDate, documentId);
        await db.ClientIdentityDocuments.AddAsync(document, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToIdentityDocumentDto(document);
    }

    public async Task<IReadOnlyList<ClientIdentityDocumentDto>> GetIdentityDocumentsAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default)
    {
        var documents = await db.ClientIdentityDocuments.Where(d => d.TenantId == tenantId && d.ClientId == clientId).ToListAsync(cancellationToken);
        return documents.Select(ToIdentityDocumentDto).ToList();
    }

    public async Task<ClientIdentityDocumentDto> VerifyIdentityDocumentAsync(Guid tenantId, Guid clientId, Guid documentId, Guid verifiedBy, bool approve, CancellationToken cancellationToken = default)
    {
        var document = await db.ClientIdentityDocuments.SingleOrDefaultAsync(d => d.TenantId == tenantId && d.ClientId == clientId && d.Id == documentId, cancellationToken)
            ?? throw new NotFoundException(nameof(ClientIdentityDocument), documentId);

        if (approve)
        {
            // Module 3 Validation Rules: "identity doc expiry must be future for 'Verified' status".
            if (document.ExpiryDate.HasValue && document.ExpiryDate.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
            {
                throw new ConflictException("Cannot verify an identity document that has already expired.", "IDENTITY_DOCUMENT_EXPIRED");
            }

            document.Verify(verifiedBy);
        }
        else
        {
            document.Reject(verifiedBy);
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToIdentityDocumentDto(document);
    }

    public async Task<ClientRelationshipDto> AddRelationshipAsync(Guid tenantId, Guid clientId, Guid? relatedClientId, string? personName, string relationType, CancellationToken cancellationToken = default)
    {
        await GetOrThrowAsync(tenantId, clientId, cancellationToken);
        var relationship = new ClientRelationship(tenantId, clientId, relatedClientId, personName, relationType);
        await db.ClientRelationships.AddAsync(relationship, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToRelationshipDto(relationship);
    }

    public async Task<IReadOnlyList<ClientRelationshipDto>> GetRelationshipsAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default)
    {
        var relationships = await db.ClientRelationships.Where(r => r.TenantId == tenantId && r.ClientId == clientId).ToListAsync(cancellationToken);
        return relationships.Select(ToRelationshipDto).ToList();
    }

    public async Task<ClientSummaryDto> GetSummaryAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default)
    {
        await GetOrThrowAsync(tenantId, clientId, cancellationToken);

        // legal.matters/fin.invoices/fin.trust_ledger_entries don't exist in this build yet
        // (04_Legal/06_Fin ship in later prompts) — to_regclass lets these no-op safely until
        // then, same forward-compatible pattern as crm.clients' delete-block trigger.
        var openMatters = (int)await CountReferencingRowsAsync("legal.matters", "client_id", clientId, cancellationToken, "status NOT IN ('Closed','Disposed') AND ");
        var lifetimeBilled = await SumReferencingRowsAsync("fin.invoices", "client_id", "total", clientId, cancellationToken);
        var outstanding = await SumReferencingRowsAsync("fin.invoices", "client_id", "balance_due", clientId, cancellationToken, "status <> 'Paid' AND ");
        var trustBalance = await SumReferencingRowsAsync("fin.trust_accounts", "client_id", "running_balance", clientId, cancellationToken);

        return new ClientSummaryDto(openMatters, lifetimeBilled, outstanding, trustBalance, NextHearing: null, DocumentCount: 0, InvoiceCount: 0);
    }

    public async Task<ClientPortalUserDto> SetPortalAccessAsync(Guid tenantId, Guid clientId, bool enable, CancellationToken cancellationToken = default)
    {
        var client = await GetOrThrowAsync(tenantId, clientId, cancellationToken);

        if (enable && string.IsNullOrWhiteSpace(client.Email))
        {
            throw new ConflictException("Portal enable requires a verified email address on the client.", "CLIENT_EMAIL_REQUIRED");
        }

        var portalUser = await db.ClientPortalUsers.SingleOrDefaultAsync(p => p.TenantId == tenantId && p.ClientId == clientId, cancellationToken);
        if (portalUser is null && enable)
        {
            portalUser = new ClientPortalUser(tenantId, clientId, client.Email!, client.DisplayName);
            await db.ClientPortalUsers.AddAsync(portalUser, cancellationToken);
        }
        else if (portalUser is not null)
        {
            if (enable)
            {
                portalUser.Reinvite();
            }
            else
            {
                portalUser.Disable();
            }
        }

        client.SetPortalEnabled(enable);
        await db.SaveChangesAsync(cancellationToken);

        return portalUser is null
            ? new ClientPortalUserDto(Guid.Empty, clientId, string.Empty, "Deactivated", false)
            : new ClientPortalUserDto(portalUser.Id, clientId, portalUser.Email, portalUser.Status, portalUser.TwoFaEnabled);
    }

    public async Task ResendPortalInviteAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default)
    {
        var portalUser = await db.ClientPortalUsers.SingleOrDefaultAsync(p => p.TenantId == tenantId && p.ClientId == clientId, cancellationToken)
            ?? throw new NotFoundException(nameof(ClientPortalUser), clientId);

        portalUser.Reinvite();
        await db.SaveChangesAsync(cancellationToken);

        // Actual invite email delivery is a Communication-module concern (Module 11), out of
        // scope here — same "issue the token/state change, defer delivery" split already used
        // by IUserManagementService.InviteAsync's purpose-token issuance.
    }

    public async Task MergeAsync(Guid tenantId, Guid survivorId, Guid duplicateId, IReadOnlyDictionary<string, string> fieldChoices, CancellationToken cancellationToken = default)
    {
        if (survivorId == duplicateId)
        {
            throw new ConflictException("Cannot merge a client into itself.", "MERGE_SAME_CLIENT");
        }

        var survivor = await GetOrThrowAsync(tenantId, survivorId, cancellationToken);
        var duplicate = await GetOrThrowAsync(tenantId, duplicateId, cancellationToken);

        // BeginTransactionAsync is relational-only — no-ops under the EF InMemory provider
        // (unit tests); against Postgres this wraps the full re-parent for AC-C3 atomicity.
        var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;

        string? Pick(string key, string? existing) => fieldChoices.TryGetValue(key, out var v) ? v : existing;

        survivor.Update(
            Pick("firstName", survivor.FirstName),
            Pick("lastName", survivor.LastName),
            Pick("legalName", survivor.LegalName),
            Pick("email", survivor.Email),
            Pick("phoneE164", survivor.PhoneE164),
            Pick("gstin", survivor.Gstin),
            Pick("cin", survivor.Cin),
            survivor.CreditLimit,
            survivor.OwnerId,
            survivor.BranchId);

        // AC-C3: re-parent 100% of contacts/addresses/identity-documents/relationships.
        var contacts = await db.ClientContacts.Where(c => c.TenantId == tenantId && c.ClientId == duplicateId).ToListAsync(cancellationToken);
        foreach (var contact in contacts)
        {
            contact.Reparent(survivorId);
        }

        var addresses = await db.ClientAddresses.Where(a => a.TenantId == tenantId && a.ClientId == duplicateId).ToListAsync(cancellationToken);
        foreach (var address in addresses)
        {
            address.Reparent(survivorId);
        }

        var identityDocuments = await db.ClientIdentityDocuments.Where(d => d.TenantId == tenantId && d.ClientId == duplicateId).ToListAsync(cancellationToken);
        foreach (var document in identityDocuments)
        {
            document.Reparent(survivorId);
        }

        var relationshipsFrom = await db.ClientRelationships.Where(r => r.TenantId == tenantId && r.ClientId == duplicateId).ToListAsync(cancellationToken);
        foreach (var relationship in relationshipsFrom)
        {
            relationship.Reparent(survivorId);
        }

        var relationshipsTo = await db.ClientRelationships.Where(r => r.TenantId == tenantId && r.RelatedClientId == duplicateId).ToListAsync(cancellationToken);
        foreach (var relationship in relationshipsTo)
        {
            relationship.RepointRelatedClient(survivorId);
        }

        // Edge case: "merge where both have portal users (survivor keeps, duplicate's portal
        // login disabled with redirect email)"; if only the duplicate has one, transfer it.
        var survivorPortal = await db.ClientPortalUsers.SingleOrDefaultAsync(p => p.TenantId == tenantId && p.ClientId == survivorId, cancellationToken);
        var duplicatePortal = await db.ClientPortalUsers.SingleOrDefaultAsync(p => p.TenantId == tenantId && p.ClientId == duplicateId, cancellationToken);
        if (duplicatePortal is not null)
        {
            if (survivorPortal is not null)
            {
                duplicatePortal.Disable();
            }
            else
            {
                duplicatePortal.Reparent(survivorId);
            }
        }

        // legal.matters/fin.invoices/dms.documents don't exist in this build yet — the
        // to_regclass-gated UPDATE no-ops safely until those schemas ship (same
        // forward-compatible pattern as crm.clients' delete-block trigger).
        await ReparentIfTableExistsAsync("legal.matters", "client_id", duplicateId, survivorId, cancellationToken);
        await ReparentIfTableExistsAsync("fin.invoices", "client_id", duplicateId, survivorId, cancellationToken);
        await ReparentIfTableExistsAsync("dms.documents", "client_id", duplicateId, survivorId, cancellationToken);

        duplicate.MarkMergedInto(survivorId);

        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            await transaction.DisposeAsync();
        }
    }

    public Task<IReadOnlyList<ClientCommunicationDto>> GetCommunicationsAsync(Guid tenantId, Guid clientId, string? channel, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken = default)
    {
        // comm.* schema (Module 11) doesn't exist in this build yet.
        return Task.FromResult<IReadOnlyList<ClientCommunicationDto>>([]);
    }

    private async Task ClearExistingPrimaryOfKindAsync(Guid tenantId, Guid clientId, string kind, CancellationToken cancellationToken, Guid? excludeId = null)
    {
        var existing = await db.ClientAddresses
            .Where(a => a.TenantId == tenantId && a.ClientId == clientId && a.Kind == kind && a.IsPrimaryOfKind && a.Id != excludeId)
            .ToListAsync(cancellationToken);

        foreach (var address in existing)
        {
            address.SetPrimary(false);
        }
    }

    private async Task ReparentIfTableExistsAsync(string table, string column, Guid fromId, Guid toId, CancellationToken cancellationToken)
    {
        // to_regclass/raw SQL is Postgres-only — the EF InMemory provider (used by unit tests)
        // has no real ADO.NET connection to run it against; no-op there, same as when the
        // target schema genuinely doesn't exist yet.
        if (!db.Database.IsRelational())
        {
            return;
        }

        var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"UPDATE {table} SET {column} = @toId WHERE {column} = @fromId AND to_regclass('{table}') IS NOT NULL";
        AddParameter(command, "toId", toId);
        AddParameter(command, "fromId", fromId);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (DbException)
        {
            // Table genuinely doesn't exist yet in this environment (to_regclass in the WHERE
            // clause still requires the statement to parse against a real table reference) —
            // safe to ignore, mirrors the DB-side to_regclass guard pattern used elsewhere.
        }
    }

    private async Task<long> CountReferencingRowsAsync(string table, string column, Guid clientId, CancellationToken cancellationToken, string? extraWhere = null)
    {
        if (!db.Database.IsRelational())
        {
            return 0;
        }

        var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $@"
            SELECT CASE WHEN to_regclass('{table}') IS NULL THEN 0
            ELSE (SELECT count(*) FROM {table} WHERE {extraWhere}{column} = @clientId AND is_deleted = false) END";
        AddParameter(command, "clientId", clientId);

        try
        {
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(result);
        }
        catch (DbException)
        {
            return 0;
        }
    }

    private async Task<decimal> SumReferencingRowsAsync(string table, string column, string sumColumn, Guid clientId, CancellationToken cancellationToken, string? extraWhere = null)
    {
        if (!db.Database.IsRelational())
        {
            return 0;
        }

        var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $@"
            SELECT CASE WHEN to_regclass('{table}') IS NULL THEN 0
            ELSE (SELECT coalesce(sum({sumColumn}), 0) FROM {table} WHERE {extraWhere}{column} = @clientId AND is_deleted = false) END";
        AddParameter(command, "clientId", clientId);

        try
        {
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToDecimal(result);
        }
        catch (DbException)
        {
            return 0;
        }
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        return connection;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private async Task<Client> GetOrThrowAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken)
        => await db.Clients.SingleOrDefaultAsync(c => c.TenantId == tenantId && c.Id == clientId, cancellationToken)
           ?? throw new NotFoundException(nameof(Client), clientId);

    private async Task<string> GenerateNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var count = await db.Clients.IgnoreQueryFilters().CountAsync(c => c.TenantId == tenantId, cancellationToken);
        return $"CL-{DateTimeOffset.UtcNow.Year}-{count + 1:D6}";
    }

    private static ClientContactDto ToContactDto(ClientContact c) => new(c.Id, c.ClientId, c.Name, c.Designation, c.Email, c.Phone, c.IsPrimary);

    private static ClientAddressDto ToAddressDto(ClientAddress a) => new(a.Id, a.ClientId, a.Kind, a.Line1, a.Line2, a.City, a.StateCode, a.Postal, a.Country, a.IsPrimaryOfKind);

    private static ClientIdentityDocumentDto ToIdentityDocumentDto(ClientIdentityDocument d) => new(d.Id, d.ClientId, d.DocKind, d.Last4, d.ExpiryDate, d.VerifyStatus, d.VerifiedBy, d.VerifiedAt);

    private static ClientRelationshipDto ToRelationshipDto(ClientRelationship r) => new(r.Id, r.ClientId, r.RelatedClientId, r.PersonName, r.RelationType);

    private static ClientDto ToDto(Client c) => new(
        c.Id, c.Number, c.Type, c.FirstName, c.LastName, c.LegalName, c.DisplayName, c.Email, c.PhoneE164, c.Gstin, c.Cin,
        c.Status, c.CreditLimit, c.OwnerId, c.BranchId, c.PortalEnabled, c.SourceLeadId, c.MergedIntoClientId, c.CreatedAt);
}
