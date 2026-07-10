using FluentAssertions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Ai;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Ai;

/// <summary>
/// Module 16 Security: "RBAC-filtered retrieval (test-enforced)" — retrieval must call the same
/// permission-check used by the record's normal read endpoint. AC-AI1's core guarantee lives
/// here: a user without access to a record must never have it deemed accessible.
/// </summary>
public sealed class AiRetrievalGuardTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task CanAccessAsync_Document_reuses_IDocumentService_GetByIdAsync_directly()
    {
        await using var db = CreateContext(nameof(CanAccessAsync_Document_reuses_IDocumentService_GetByIdAsync_directly));
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var documentService = new FakeDocumentService(new DocumentDto(documentId, "Title", "General", "Internal", null, null, null, null, null, false, DateTimeOffset.UtcNow));
        var guard = new AiRetrievalGuard(db, new FakePermissionService([]), documentService, new FakeKbArticleService());

        var accessible = await guard.CanAccessAsync(tenantId, Guid.NewGuid(), [], "Document", documentId, CancellationToken.None);

        accessible.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessAsync_Document_denies_when_the_underlying_service_returns_null()
    {
        await using var db = CreateContext(nameof(CanAccessAsync_Document_denies_when_the_underlying_service_returns_null));
        var documentService = new FakeDocumentService(null);
        var guard = new AiRetrievalGuard(db, new FakePermissionService([]), documentService, new FakeKbArticleService());

        var accessible = await guard.CanAccessAsync(Guid.NewGuid(), Guid.NewGuid(), [], "Document", Guid.NewGuid(), CancellationToken.None);

        accessible.Should().BeFalse();
    }

    [Fact]
    public async Task CanAccessAsync_Privileged_document_is_denied_when_the_owning_matter_row_does_not_exist()
    {
        await using var db = CreateContext(nameof(CanAccessAsync_Privileged_document_is_denied_when_the_owning_matter_row_does_not_exist));
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        // Matter.Id below deliberately has no corresponding row in db.Matters.
        var documentService = new FakeDocumentService(new DocumentDto(documentId, "Privileged Doc", "General", "Privileged", null, Guid.NewGuid(), null, null, null, false, DateTimeOffset.UtcNow));
        var guard = new AiRetrievalGuard(db, new FakePermissionService([]), documentService, new FakeKbArticleService());

        var accessible = await guard.CanAccessAsync(tenantId, Guid.NewGuid(), ["document.privileged.read"], "Document", documentId, CancellationToken.None);

        accessible.Should().BeFalse("a Privileged document whose owning matter can't be resolved must never be treated as AI-accessible");
    }

    [Fact]
    public async Task CanAccessAsync_Privileged_document_is_accessible_when_the_owning_matter_opts_in()
    {
        await using var db = CreateContext(nameof(CanAccessAsync_Privileged_document_is_accessible_when_the_owning_matter_opts_in));
        var tenantId = Guid.NewGuid();
        var matter = new Matter(tenantId, "M-1", "Matter", Guid.NewGuid(), "Litigation", null, null, null, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        await db.Matters.AddAsync(matter);
        await db.SaveChangesAsync();

        var documentId = Guid.NewGuid();
        var documentService = new FakeDocumentService(new DocumentDto(documentId, "Privileged Doc", "General", "Privileged", null, matter.Id, null, null, null, false, DateTimeOffset.UtcNow));
        var guard = new AiRetrievalGuard(db, new FakePermissionService([]), documentService, new FakeKbArticleService());

        var accessible = await guard.CanAccessAsync(tenantId, Guid.NewGuid(), ["document.privileged.read"], "Document", documentId, CancellationToken.None);

        accessible.Should().BeTrue("Matter.AiAllowed defaults to true at construction (Module 4), so a Privileged document on a matter that never opted out is AI-accessible");
    }

    [Fact]
    public async Task CanAccessAsync_Matter_own_scope_only_grants_the_responsible_lawyer()
    {
        await using var db = CreateContext(nameof(CanAccessAsync_Matter_own_scope_only_grants_the_responsible_lawyer));
        var tenantId = Guid.NewGuid();
        var lawyerId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        var matter = new Matter(tenantId, "M-1", "Matter", Guid.NewGuid(), "Litigation", null, null, lawyerId, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        await db.Matters.AddAsync(matter);
        await db.SaveChangesAsync();

        var permissions = new FakePermissionService([new EffectivePermission("matters.read.own", "matters", "read", "own")]);
        var guard = new AiRetrievalGuard(db, permissions, new FakeDocumentService(null), new FakeKbArticleService());

        var lawyerCanAccess = await guard.CanAccessAsync(tenantId, lawyerId, [], "Matter", matter.Id, CancellationToken.None);
        var strangerCanAccess = await guard.CanAccessAsync(tenantId, strangerId, [], "Matter", matter.Id, CancellationToken.None);

        lawyerCanAccess.Should().BeTrue();
        strangerCanAccess.Should().BeFalse();
    }

    [Fact]
    public async Task CanAccessAsync_KbJudgment_requires_kb_read_all_and_the_row_to_exist()
    {
        await using var db = CreateContext(nameof(CanAccessAsync_KbJudgment_requires_kb_read_all_and_the_row_to_exist));
        var tenantId = Guid.NewGuid();
        var judgment = new KbJudgment(tenantId, "AIR 2024 SC 1", null, null, null, "A v B", "Headnote", null);
        await db.KbJudgments.AddAsync(judgment);
        await db.SaveChangesAsync();

        var guard = new AiRetrievalGuard(db, new FakePermissionService([]), new FakeDocumentService(null), new FakeKbArticleService());

        var withoutPermission = await guard.CanAccessAsync(tenantId, Guid.NewGuid(), [], "KbJudgment", judgment.Id, CancellationToken.None);
        var withPermission = await guard.CanAccessAsync(tenantId, Guid.NewGuid(), ["kb.read.all"], "KbJudgment", judgment.Id, CancellationToken.None);
        var nonexistent = await guard.CanAccessAsync(tenantId, Guid.NewGuid(), ["kb.read.all"], "KbJudgment", Guid.NewGuid(), CancellationToken.None);

        withoutPermission.Should().BeFalse();
        withPermission.Should().BeTrue();
        nonexistent.Should().BeFalse();
    }

    [Fact]
    public async Task CanAccessAsync_unknown_source_kind_is_denied()
    {
        await using var db = CreateContext(nameof(CanAccessAsync_unknown_source_kind_is_denied));
        var guard = new AiRetrievalGuard(db, new FakePermissionService([]), new FakeDocumentService(null), new FakeKbArticleService());

        var accessible = await guard.CanAccessAsync(Guid.NewGuid(), Guid.NewGuid(), ["everything"], "SomethingElse", Guid.NewGuid(), CancellationToken.None);

        accessible.Should().BeFalse();
    }

    private sealed class FakeDocumentService(DocumentDto? result) : IDocumentService
    {
        public Task<DocumentDto> CreateAsync(Guid tenantId, Guid? actorId, CreateDocumentInput input, byte[] fileContent, string fileName, string? mime, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<DocumentDto?> GetByIdAsync(Guid tenantId, Guid documentId, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default) => Task.FromResult(result);

        public Task<IReadOnlyList<DocumentDto>> GetAllAsync(Guid tenantId, DocumentFilter filter, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<DocumentDto> UpdateMetadataAsync(Guid tenantId, Guid documentId, string title, string docType, string confidentiality, Guid? folderId, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task DeleteAsync(Guid tenantId, Guid documentId, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<DocumentVersionDto> AddVersionAsync(Guid tenantId, Guid? actorId, Guid documentId, byte[] fileContent, string fileName, string? mime, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IReadOnlyList<DocumentVersionDto>> GetVersionsAsync(Guid tenantId, Guid documentId, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<DocumentVersionDto> RestoreVersionAsync(Guid tenantId, Guid? actorId, Guid documentId, int versionNo, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<string> GetDownloadUrlAsync(Guid tenantId, Guid? actorId, Guid documentId, string? ip, string? userAgent, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task LogActivityAsync(Guid tenantId, Guid? actorId, Guid documentId, string action, string? ip, string? userAgent, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task BulkActionAsync(Guid tenantId, Guid? actorId, string action, IReadOnlyList<Guid> documentIds, Guid? targetFolderId, IReadOnlyList<string>? tagNames, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FakeKbArticleService : IKbArticleService
    {
        public Task<KbArticleDto> CreateDraftAsync(Guid tenantId, Guid authorId, string title, string? body, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<KbArticleDto> UpdateDraftAsync(Guid tenantId, Guid articleId, string title, string? body, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<KbArticleDto> SubmitForReviewAsync(Guid tenantId, Guid articleId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<KbArticleDto> AssignReviewerAsync(Guid tenantId, Guid articleId, Guid reviewerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<KbArticleDto> SendBackToDraftAsync(Guid tenantId, Guid articleId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<KbArticleDto> PublishAsync(Guid tenantId, Guid articleId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<KbArticleDto?> GetAsync(Guid tenantId, Guid articleId, Guid callerId, bool callerCanReview, CancellationToken cancellationToken = default) => Task.FromResult<KbArticleDto?>(null);

        public Task<IReadOnlyList<KbArticleDto>> GetVisibleAsync(Guid tenantId, Guid callerId, bool callerCanReview, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IReadOnlyList<KbArticleVersionDto>> GetVersionsAsync(Guid tenantId, Guid articleId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FakePermissionService(IReadOnlyCollection<EffectivePermission> permissions) : IPermissionService
    {
        public Task<IReadOnlyCollection<EffectivePermission>> GetEffectivePermissionsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult(permissions);

        public Task InvalidateAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<PermissionCatalogItem>> GetCatalogAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PermissionCatalogItem>>([]);

        public Task<IReadOnlyList<EffectivePermissionExplanation>> ExplainEffectivePermissionsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<EffectivePermissionExplanation>>([]);
    }
}
