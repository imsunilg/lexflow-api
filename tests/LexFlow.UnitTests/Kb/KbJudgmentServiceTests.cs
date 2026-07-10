using FluentAssertions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Kb;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Kb;

/// <summary>Module 12: judgment upload pipeline — AV scan, best-effort text extraction with graceful OCR-style fallback, duplicate-citation handling.</summary>
public sealed class KbJudgmentServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private static KbJudgmentService CreateService(LexFlowDbContext db, FakeAvScanner? av = null, FakeTextExtractionService? extraction = null, FakeKbSearchIndexer? indexer = null)
        => new(db, new FakeBlobStorageService(), av ?? new FakeAvScanner(clean: true), extraction ?? new FakeTextExtractionService(success: true, text: "Full judgment text."), indexer ?? new FakeKbSearchIndexer());

    [Fact]
    public async Task UploadAsync_creates_the_judgment_and_indexes_it()
    {
        await using var db = CreateContext(nameof(UploadAsync_creates_the_judgment_and_indexes_it));
        var indexer = new FakeKbSearchIndexer();
        var service = CreateService(db, indexer: indexer);
        var tenantId = Guid.NewGuid();

        var result = await service.UploadAsync(tenantId, Guid.NewGuid(), new UploadJudgmentInput("(2023) 5 SCC 1", null, null, new DateOnly(2023, 3, 1), "A v. B", "Headnote text"), [1, 2, 3], "judgment.pdf", "application/pdf", CancellationToken.None);

        result.Citation.Should().Be("(2023) 5 SCC 1");
        result.OcrStatus.Should().Be("Done");
        result.DocumentId.Should().NotBeNull();
        indexer.IndexedDocs.Should().ContainSingle(d => d.Kind == "Judgment" && d.Id == result.Id);
    }

    [Fact]
    public async Task UploadAsync_throws_DUPLICATE_CITATION_for_a_repeat_citation()
    {
        await using var db = CreateContext(nameof(UploadAsync_throws_DUPLICATE_CITATION_for_a_repeat_citation));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();
        await service.UploadAsync(tenantId, Guid.NewGuid(), new UploadJudgmentInput("AIR 2020 SC 123", null, null, null, null, null), [1], "a.pdf", "application/pdf", CancellationToken.None);

        var act = () => service.UploadAsync(tenantId, Guid.NewGuid(), new UploadJudgmentInput("AIR 2020 SC 123", null, null, null, null, null), [1], "b.pdf", "application/pdf", CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().Where(e => e.Code == "DUPLICATE_CITATION");
    }

    [Fact]
    public async Task UploadAsync_throws_MALWARE_DETECTED_on_a_positive_AV_scan()
    {
        await using var db = CreateContext(nameof(UploadAsync_throws_MALWARE_DETECTED_on_a_positive_AV_scan));
        var service = CreateService(db, av: new FakeAvScanner(clean: false));
        var tenantId = Guid.NewGuid();

        var act = () => service.UploadAsync(tenantId, Guid.NewGuid(), new UploadJudgmentInput("AIR 2021 SC 1", null, null, null, null, null), [1], "a.pdf", "application/pdf", CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "MALWARE_DETECTED");
    }

    [Fact]
    public async Task UploadAsync_falls_back_to_metadata_only_searchable_when_extraction_yields_no_text()
    {
        await using var db = CreateContext(nameof(UploadAsync_falls_back_to_metadata_only_searchable_when_extraction_yields_no_text));
        var service = CreateService(db, extraction: new FakeTextExtractionService(success: true, text: "   "));
        var tenantId = Guid.NewGuid();

        var result = await service.UploadAsync(tenantId, Guid.NewGuid(), new UploadJudgmentInput("AIR 2022 SC 5", null, null, null, null, "Headnote only"), [1], "scan.pdf", "application/pdf", CancellationToken.None);

        result.OcrStatus.Should().Be("Failed");
    }

    [Fact]
    public async Task RetryExtractionAsync_updates_the_ocr_status_after_a_successful_retry()
    {
        await using var db = CreateContext(nameof(RetryExtractionAsync_updates_the_ocr_status_after_a_successful_retry));
        var extraction = new FakeTextExtractionService(success: true, text: "");
        var service = CreateService(db, extraction: extraction);
        var tenantId = Guid.NewGuid();
        var judgment = await service.UploadAsync(tenantId, Guid.NewGuid(), new UploadJudgmentInput("AIR 2022 SC 9", null, null, null, null, null), [1], "scan.pdf", "application/pdf", CancellationToken.None);
        judgment.OcrStatus.Should().Be("Failed");

        extraction.Text = "Recovered text on retry.";
        await service.RetryExtractionAsync(tenantId, judgment.Id, CancellationToken.None);

        var refreshed = await service.GetAsync(tenantId, judgment.Id, CancellationToken.None);
        refreshed!.OcrStatus.Should().Be("Done");
    }

    [Fact]
    public async Task GetPinCountAsync_counts_distinct_pins_for_the_judgment()
    {
        await using var db = CreateContext(nameof(GetPinCountAsync_counts_distinct_pins_for_the_judgment));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();
        var judgment = await service.UploadAsync(tenantId, Guid.NewGuid(), new UploadJudgmentInput("AIR 2023 SC 7", null, null, null, null, "Headnote"), [1], "a.pdf", "application/pdf", CancellationToken.None);

        await db.KbMatterPins.AddAsync(new LexFlow.Domain.Entities.KbMatterPin(tenantId, Guid.NewGuid(), "Judgment", judgment.Id, null, "snapshot", null));
        await db.KbMatterPins.AddAsync(new LexFlow.Domain.Entities.KbMatterPin(tenantId, Guid.NewGuid(), "Judgment", judgment.Id, null, "snapshot", null));
        await db.SaveChangesAsync();

        var count = await service.GetPinCountAsync(tenantId, judgment.Id, CancellationToken.None);

        count.Should().Be(2);
    }

    private sealed class FakeAvScanner(bool clean) : IAvScanner
    {
        public Task<AvScanResult> ScanAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
            => Task.FromResult(new AvScanResult(clean, clean ? null : "EICAR-Test-Signature"));
    }

    private sealed class FakeTextExtractionService(bool success, string text) : ITextExtractionService
    {
        public string Text { get; set; } = text;

        public Task<TextExtractionResult> ExtractAsync(byte[] content, string? mime, CancellationToken cancellationToken = default)
            => Task.FromResult(new TextExtractionResult(success, Text, success ? "Done" : "Failed"));
    }

    private sealed class FakeBlobStorageService : IBlobStorageService
    {
        private readonly Dictionary<string, byte[]> _store = [];

        public Task<string> UploadAsync(string container, string blobPath, byte[] content, string contentType, CancellationToken cancellationToken = default)
        {
            _store[$"{container}/{blobPath}"] = content;
            return Task.FromResult(blobPath);
        }

        public Task<byte[]> DownloadAsync(string container, string blobPath, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.GetValueOrDefault($"{container}/{blobPath}", []));

        public Task<string> GetDownloadUrlAsync(string container, string blobPath, TimeSpan validFor, CancellationToken cancellationToken = default)
            => Task.FromResult($"https://fake-blob.test/{container}/{blobPath}");
    }

    private sealed class FakeKbSearchIndexer : IKbSearchIndexer
    {
        public List<KbSearchDoc> IndexedDocs { get; } = [];

        public Task IndexAsync(Guid tenantId, KbSearchDoc doc, CancellationToken cancellationToken = default)
        {
            IndexedDocs.Add(doc);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid tenantId, string kind, Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<KbSearchHit>> SearchAsync(Guid tenantId, KbSearchQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<KbSearchHit>>([]);
    }
}
