using FluentAssertions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Ai;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Ai;

/// <summary>Module 16 features 1-9/12 pipeline: quota gate, RBAC-filtered RAG context, citation verification against a hallucinated reference, and the AI-generated badge on every response.</summary>
public sealed class AiAssistantServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private static AiAssistantService CreateService(
        LexFlowDbContext db,
        ILlmProvider? llmProvider = null,
        IRagRetrievalService? rag = null,
        IAiRetrievalGuard? guard = null,
        IAiQuotaService? quota = null,
        IDocumentService? documentService = null,
        IMatterService? matterService = null)
    {
        var retrievalGuard = guard ?? new AllowAllGuard();
        return new AiAssistantService(
            db,
            llmProvider ?? new FakeLlmProvider("A plain AI answer."),
            new AiPromptTemplateService(),
            rag ?? new FakeRagRetrievalService([]),
            retrievalGuard,
            new CitationVerifier(retrievalGuard),
            quota ?? new AllowAllQuotaService(),
            new AiInteractionAuditService(db),
            documentService ?? new FakeDocumentService(null),
            matterService ?? new FakeMatterService(null),
            new FakeBlobStorageService(),
            new FakeTextExtractionService());
    }

    [Fact]
    public async Task ChatAsync_throws_AiQuotaExceededException_when_the_tenant_is_over_quota()
    {
        await using var db = CreateContext(nameof(ChatAsync_throws_AiQuotaExceededException_when_the_tenant_is_over_quota));
        var service = CreateService(db, quota: new AlwaysExceededQuotaService());

        var act = () => service.ChatAsync(Guid.NewGuid(), Guid.NewGuid(), [], new AiChatRequest("Hello", null, null, null, null), CancellationToken.None);

        await act.Should().ThrowAsync<AiQuotaExceededException>();
    }

    [Fact]
    public async Task ChatAsync_strips_a_citation_the_model_invented_that_the_guard_denies()
    {
        await using var db = CreateContext(nameof(ChatAsync_strips_a_citation_the_model_invented_that_the_guard_denies));
        var fabricatedId = Guid.NewGuid();
        var llm = new FakeLlmProvider($"Per [REF:KbJudgment:{fabricatedId}] the claim holds.");
        var service = CreateService(db, llmProvider: llm, guard: new DenyAllGuard());

        var response = await service.ChatAsync(Guid.NewGuid(), Guid.NewGuid(), [], new AiChatRequest("Is this true?", null, null, null, null), CancellationToken.None);

        response.Citations.Should().BeEmpty("a citation token the retrieval guard denies must never be surfaced as verified");
        response.IsAiGenerated.Should().BeTrue();
    }

    [Fact]
    public async Task ChatAsync_keeps_a_citation_the_guard_confirms_is_real_and_accessible()
    {
        await using var db = CreateContext(nameof(ChatAsync_keeps_a_citation_the_guard_confirms_is_real_and_accessible));
        var realId = Guid.NewGuid();
        var llm = new FakeLlmProvider($"Per [REF:KbJudgment:{realId}] the claim holds.");
        var service = CreateService(db, llmProvider: llm, guard: new AllowAllGuard());

        var response = await service.ChatAsync(Guid.NewGuid(), Guid.NewGuid(), [], new AiChatRequest("Is this true?", null, null, null, null), CancellationToken.None);

        response.Citations.Should().ContainSingle(c => c.Id == realId && c.Kind == "KbJudgment");
    }

    [Fact]
    public async Task SummarizeDocumentAsync_throws_NotFound_when_the_retrieval_guard_denies_the_document()
    {
        await using var db = CreateContext(nameof(SummarizeDocumentAsync_throws_NotFound_when_the_retrieval_guard_denies_the_document));
        var service = CreateService(db, guard: new DenyAllGuard());

        var act = () => service.SummarizeDocumentAsync(Guid.NewGuid(), Guid.NewGuid(), [], Guid.NewGuid(), null, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Every_response_from_a_successful_call_carries_the_AI_generated_badge()
    {
        await using var db = CreateContext(nameof(Every_response_from_a_successful_call_carries_the_AI_generated_badge));
        var service = CreateService(db);

        var response = await service.ChatAsync(Guid.NewGuid(), Guid.NewGuid(), [], new AiChatRequest("Hello", null, null, null, null), CancellationToken.None);

        response.IsAiGenerated.Should().BeTrue();
        response.Disclaimer.Should().NotBeNullOrWhiteSpace();
    }

    private sealed class FakeLlmProvider(string text) : ILlmProvider
    {
        public string ProviderName => "fake";

        public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new LlmCompletionResult(text, 10, 10, request.Model));
    }

    private sealed class FakeRagRetrievalService(IReadOnlyList<RagChunk> chunks) : IRagRetrievalService
    {
        public Task<IReadOnlyList<RagChunk>> RetrieveAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, string query, int topK, IReadOnlyCollection<string>? sourceKinds = null, CancellationToken cancellationToken = default)
            => Task.FromResult(chunks);

        public Task IndexAsync(Guid tenantId, string sourceKind, Guid sourceId, string text, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class AllowAllGuard : IAiRetrievalGuard
    {
        public Task<bool> CanAccessAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, string sourceKind, Guid sourceId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class DenyAllGuard : IAiRetrievalGuard
    {
        public Task<bool> CanAccessAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, string sourceKind, Guid sourceId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class AllowAllQuotaService : IAiQuotaService
    {
        public Task EnsureWithinQuotaAsync(Guid tenantId, decimal estimatedCredits, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<AiQuotaStatus> GetStatusAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(new AiQuotaStatus(1000, 0, 1000));
    }

    private sealed class AlwaysExceededQuotaService : IAiQuotaService
    {
        public Task EnsureWithinQuotaAsync(Guid tenantId, decimal estimatedCredits, CancellationToken cancellationToken = default) => throw new AiQuotaExceededException(100, 100);

        public Task<AiQuotaStatus> GetStatusAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(new AiQuotaStatus(100, 100, 0));
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

    private sealed class FakeMatterService(MatterDto? result) : IMatterService
    {
        public Task<MatterDto> CreateAsync(Guid tenantId, Guid? actorId, CreateMatterInput input, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<MatterDto?> GetByIdAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default) => Task.FromResult(result);

        public Task<IReadOnlyList<MatterDto>> GetAllAsync(Guid tenantId, MatterFilter filter, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<MatterDto> UpdateAsync(Guid tenantId, Guid matterId, UpdateMatterInput input, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<MatterDto> ChangeStatusAsync(Guid tenantId, Guid? actorId, Guid matterId, string toStatus, string? outcome, string? closureNote, bool allowReopen, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task AddTeamMemberAsync(Guid tenantId, Guid matterId, Guid userId, string? roleInMatter, decimal? rateOverride, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task RemoveTeamMemberAsync(Guid tenantId, Guid matterId, Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IReadOnlyList<MatterTeamMemberDto>> GetTeamAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<MatterPartyDto> AddPartyAsync(Guid tenantId, Guid matterId, MatterPartyInput input, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<MatterPartyDto> UpdatePartyAsync(Guid tenantId, Guid matterId, Guid partyId, MatterPartyInput input, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task DeletePartyAsync(Guid tenantId, Guid matterId, Guid partyId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IReadOnlyList<MatterPartyDto>> GetPartiesAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<MatterImportantDateDto> AddImportantDateAsync(Guid tenantId, Guid matterId, ImportantDateInput input, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<MatterImportantDateDto> UpdateImportantDateAsync(Guid tenantId, Guid matterId, Guid dateId, ImportantDateInput input, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task DeleteImportantDateAsync(Guid tenantId, Guid matterId, Guid dateId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IReadOnlyList<MatterImportantDateDto>> GetImportantDatesAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<MatterExpenseDto> AddExpenseAsync(Guid tenantId, Guid matterId, MatterExpenseInput input, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IReadOnlyList<MatterExpenseDto>> GetExpensesAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<MatterRelatedDto> AddRelatedAsync(Guid tenantId, Guid matterId, Guid relatedMatterId, string relationType, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<TimelinePage> GetTimelineAsync(Guid tenantId, Guid matterId, string? types, string? cursor, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<MatterFinancialSummaryDto> GetFinancialSummaryAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FakeBlobStorageService : IBlobStorageService
    {
        public Task<string> UploadAsync(string container, string blobPath, byte[] content, string contentType, CancellationToken cancellationToken = default) => Task.FromResult(blobPath);

        public Task<byte[]> DownloadAsync(string container, string blobPath, CancellationToken cancellationToken = default) => Task.FromResult<byte[]>([]);

        public Task<string> GetDownloadUrlAsync(string container, string blobPath, TimeSpan validFor, CancellationToken cancellationToken = default) => Task.FromResult($"https://fake/{container}/{blobPath}");
    }

    private sealed class FakeTextExtractionService : ITextExtractionService
    {
        public Task<TextExtractionResult> ExtractAsync(byte[] content, string? mime, CancellationToken cancellationToken = default)
            => Task.FromResult(new TextExtractionResult(true, "extracted text", "Done"));
    }
}
