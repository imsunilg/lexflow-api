using System.Text;
using FluentAssertions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Crm;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Crm;

/// <summary>Module 3 duplicate-guard, KYC verify-expiry rule, and the AC-C3 merge re-parenting, against EF InMemory.</summary>
public sealed class ClientServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private static ClientService CreateService(LexFlowDbContext db) => new(db, new FakeKycEncryptionService());

    private static CreateClientInput IndividualInput(string? phone = "+919876543210", string? email = "client@example.com") =>
        new("Individual", "Priya", "Shah", null, email, phone, null, null, null, null, null, null);

    [Fact]
    public async Task CreateAsync_rejects_a_duplicate_phone_for_the_same_type()
    {
        await using var db = CreateContext(nameof(CreateAsync_rejects_a_duplicate_phone_for_the_same_type));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();

        await service.CreateAsync(tenantId, null, IndividualInput(), CancellationToken.None);

        var act = () => service.CreateAsync(tenantId, null, IndividualInput(email: "other@example.com"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().Where(e => e.Code == "CLIENT_DUPLICATE");
    }

    [Fact]
    public async Task AddIdentityDocumentAsync_never_persists_the_plaintext_document_number()
    {
        await using var db = CreateContext(nameof(AddIdentityDocumentAsync_never_persists_the_plaintext_document_number));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();
        var client = await service.CreateAsync(tenantId, null, IndividualInput(), CancellationToken.None);

        var document = await service.AddIdentityDocumentAsync(tenantId, client.Id, "PAN", "ABCDE1234F", null, null, CancellationToken.None);

        document.Last4.Should().Be("234F");
        var stored = await db.ClientIdentityDocuments.SingleAsync(d => d.Id == document.Id);
        Encoding.UTF8.GetString(stored.DocNumberEnc).Should().NotContain("ABCDE1234F");
    }

    [Fact]
    public async Task VerifyIdentityDocumentAsync_rejects_approving_an_already_expired_document()
    {
        await using var db = CreateContext(nameof(VerifyIdentityDocumentAsync_rejects_approving_an_already_expired_document));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();
        var client = await service.CreateAsync(tenantId, null, IndividualInput(), CancellationToken.None);
        var document = await service.AddIdentityDocumentAsync(tenantId, client.Id, "Passport", "P1234567", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), null, CancellationToken.None);

        var act = () => service.VerifyIdentityDocumentAsync(tenantId, client.Id, document.Id, Guid.NewGuid(), approve: true, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().Where(e => e.Code == "IDENTITY_DOCUMENT_EXPIRED");
    }

    [Fact]
    public async Task DeleteAsync_soft_deletes_when_nothing_references_the_client()
    {
        await using var db = CreateContext(nameof(DeleteAsync_soft_deletes_when_nothing_references_the_client));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();
        var client = await service.CreateAsync(tenantId, null, IndividualInput(), CancellationToken.None);

        await service.DeleteAsync(tenantId, client.Id, CancellationToken.None);

        (await service.GetByIdAsync(tenantId, client.Id, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task MergeAsync_reparents_contacts_addresses_and_relationships_and_tombstones_the_duplicate()
    {
        await using var db = CreateContext(nameof(MergeAsync_reparents_contacts_addresses_and_relationships_and_tombstones_the_duplicate));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();

        var survivor = await service.CreateAsync(tenantId, null, IndividualInput(phone: "+911111111111", email: "survivor@example.com"), CancellationToken.None);
        var duplicate = await service.CreateAsync(tenantId, null, IndividualInput(phone: "+912222222222", email: "duplicate@example.com"), CancellationToken.None);

        await service.AddContactAsync(tenantId, duplicate.Id, new ClientContactInput("Some Contact", null, null, null, false), CancellationToken.None);
        await service.AddAddressAsync(tenantId, duplicate.Id, new ClientAddressInput("Home", "221B Baker St", null, null, null, null, null, false), CancellationToken.None);
        await service.AddRelationshipAsync(tenantId, duplicate.Id, null, "Referrer Person", "Referrer", CancellationToken.None);

        await service.MergeAsync(tenantId, survivor.Id, duplicate.Id, new Dictionary<string, string>(), CancellationToken.None);

        var contacts = await service.GetContactsAsync(tenantId, survivor.Id, CancellationToken.None);
        contacts.Should().ContainSingle(c => c.Name == "Some Contact");

        var addresses = await service.GetAddressesAsync(tenantId, survivor.Id, CancellationToken.None);
        addresses.Should().ContainSingle(a => a.Line1 == "221B Baker St");

        var relationships = await service.GetRelationshipsAsync(tenantId, survivor.Id, CancellationToken.None);
        relationships.Should().ContainSingle(r => r.PersonName == "Referrer Person");

        var mergedDuplicate = await service.GetByIdAsync(tenantId, duplicate.Id, CancellationToken.None);
        mergedDuplicate.Should().NotBeNull();
        mergedDuplicate!.MergedIntoClientId.Should().Be(survivor.Id);
    }

    /// <summary>Reversible but non-identity transform (base64) — enough to prove the raw plaintext never lands in the ciphertext column, without needing a real Postgres pgcrypto connection.</summary>
    private sealed class FakeKycEncryptionService : IKycEncryptionService
    {
        public Task<byte[]> EncryptAsync(string plaintext, CancellationToken cancellationToken = default)
            => Task.FromResult(Encoding.UTF8.GetBytes(Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext))));

        public Task<string> DecryptAsync(byte[] ciphertext, CancellationToken cancellationToken = default)
            => Task.FromResult(Encoding.UTF8.GetString(Convert.FromBase64String(Encoding.UTF8.GetString(ciphertext))));
    }
}
