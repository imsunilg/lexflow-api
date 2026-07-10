using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Dms;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Dms;

/// <summary>AC-DOC3 (privileged confidentiality gating), AV-scan gate, and versioning, against EF InMemory.</summary>
public sealed class DocumentServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private static DocumentService CreateService(LexFlowDbContext db, bool avClean = true)
        => new(db, new FakeBlobStorageService(), new FakeAvScanner(avClean), new FakeBackgroundJobClient());

    private static readonly IReadOnlyCollection<string> NoPermissions = [];
    private static readonly IReadOnlyCollection<string> PrivilegedReadPermission = ["document.privileged.read"];

    private static CreateDocumentInput Input(string confidentiality = "Normal") => new(null, null, null, null, "Title", "Pleading", confidentiality, null);

    [Fact]
    public async Task CreateAsync_blocks_a_malware_positive_upload()
    {
        await using var db = CreateContext(nameof(CreateAsync_blocks_a_malware_positive_upload));
        var service = CreateService(db, avClean: false);
        var tenantId = Guid.NewGuid();

        var act = () => service.CreateAsync(tenantId, null, Input(), [1, 2, 3], "evil.pdf", "application/pdf", NoPermissions, CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "MALWARE_DETECTED");
    }

    [Fact]
    public async Task CreateAsync_blocks_creating_a_privileged_document_without_the_permission()
    {
        await using var db = CreateContext(nameof(CreateAsync_blocks_creating_a_privileged_document_without_the_permission));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();

        var act = () => service.CreateAsync(tenantId, null, Input("Privileged"), [1, 2, 3], "secret.pdf", "application/pdf", NoPermissions, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task GetByIdAsync_hides_a_privileged_document_from_a_caller_without_the_permission()
    {
        await using var db = CreateContext(nameof(GetByIdAsync_hides_a_privileged_document_from_a_caller_without_the_permission));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();
        var document = await service.CreateAsync(tenantId, null, Input("Privileged"), [1, 2, 3], "secret.pdf", "application/pdf", PrivilegedReadPermission, CancellationToken.None);

        var forRestrictedCaller = await service.GetByIdAsync(tenantId, document.Id, NoPermissions, CancellationToken.None);
        var forPrivilegedCaller = await service.GetByIdAsync(tenantId, document.Id, PrivilegedReadPermission, CancellationToken.None);

        forRestrictedCaller.Should().BeNull();
        forPrivilegedCaller.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAllAsync_excludes_privileged_documents_from_a_caller_without_the_permission()
    {
        await using var db = CreateContext(nameof(GetAllAsync_excludes_privileged_documents_from_a_caller_without_the_permission));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();
        await service.CreateAsync(tenantId, null, Input("Normal"), [1, 2, 3], "normal.pdf", "application/pdf", PrivilegedReadPermission, CancellationToken.None);
        await service.CreateAsync(tenantId, null, Input("Privileged"), [1, 2, 3], "secret.pdf", "application/pdf", PrivilegedReadPermission, CancellationToken.None);

        var results = await service.GetAllAsync(tenantId, new DocumentFilter(null, null, null, null), NoPermissions, CancellationToken.None);

        results.Should().ContainSingle();
        results.Single().Confidentiality.Should().Be("Normal");
    }

    [Fact]
    public async Task AddVersionAsync_and_RestoreVersionAsync_increment_the_version_number_and_never_rewrite_history()
    {
        await using var db = CreateContext(nameof(AddVersionAsync_and_RestoreVersionAsync_increment_the_version_number_and_never_rewrite_history));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();
        var document = await service.CreateAsync(tenantId, null, Input(), [1, 2, 3], "v1.pdf", "application/pdf", NoPermissions, CancellationToken.None);

        var v2 = await service.AddVersionAsync(tenantId, null, document.Id, [4, 5, 6], "v2.pdf", "application/pdf", NoPermissions, CancellationToken.None);
        v2.VersionNo.Should().Be(2);

        var restored = await service.RestoreVersionAsync(tenantId, null, document.Id, 1, NoPermissions, CancellationToken.None);
        restored.VersionNo.Should().Be(3);
        restored.HashSha256.Should().NotBeNullOrEmpty();

        var allVersions = await service.GetVersionsAsync(tenantId, document.Id, NoPermissions, CancellationToken.None);
        allVersions.Should().HaveCount(3, "history is never rewritten — restore appends a new version");
    }

    [Fact]
    public async Task CreateAsync_writes_an_outbox_row_in_the_same_transaction_as_the_document_insert()
    {
        await using var db = CreateContext(nameof(CreateAsync_writes_an_outbox_row_in_the_same_transaction_as_the_document_insert));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();

        var document = await service.CreateAsync(tenantId, null, Input(), [1, 2, 3], "v1.pdf", "application/pdf", NoPermissions, CancellationToken.None);

        var outboxRows = await db.DocumentIndexOutbox.Where(o => o.DocumentId == document.Id).ToListAsync();
        outboxRows.Should().ContainSingle(o => o.Operation == "Index" && o.Status == "Pending");
    }

    private sealed class FakeAvScanner(bool isClean) : IAvScanner
    {
        public Task<AvScanResult> ScanAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
            => Task.FromResult(new AvScanResult(isClean, isClean ? null : "Eicar-Test-Signature"));
    }

    private sealed class FakeBlobStorageService : IBlobStorageService
    {
        private readonly Dictionary<string, byte[]> _blobs = new();

        public Task<string> UploadAsync(string container, string blobPath, byte[] content, string contentType, CancellationToken cancellationToken = default)
        {
            _blobs[$"{container}/{blobPath}"] = content;
            return Task.FromResult(blobPath);
        }

        public Task<byte[]> DownloadAsync(string container, string blobPath, CancellationToken cancellationToken = default)
            => Task.FromResult(_blobs[$"{container}/{blobPath}"]);

        public Task<string> GetDownloadUrlAsync(string container, string blobPath, TimeSpan validFor, CancellationToken cancellationToken = default)
            => Task.FromResult($"https://fake-blob.test/{container}/{blobPath}");
    }

    private sealed class FakeBackgroundJobClient : IBackgroundJobClient
    {
        public string Create(Job job, IState state) => Guid.NewGuid().ToString();

        public bool ChangeState(string jobId, IState state, string? expectedState) => true;
    }
}
