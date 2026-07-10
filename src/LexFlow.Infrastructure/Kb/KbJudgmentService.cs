using System.Security.Cryptography;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Kb;

/// <summary>
/// Module 12: judgment upload (AV scan -&gt; blob store -&gt; a dms.documents/document_versions row,
/// same pipeline shape as Module 7's DocumentService.AddVersionInternalAsync, but owned here since
/// judgments aren't filed in DMS folders) with best-effort text extraction and OCR-style graceful
/// fallback. Validation Rules: "judgment PDF ≤ 50 MB". Edge case: "duplicate judgment upload
/// (citation match prompt merge)".
/// </summary>
public sealed class KbJudgmentService(
    LexFlowDbContext db,
    IBlobStorageService blobStorage,
    IAvScanner avScanner,
    ITextExtractionService textExtractionService,
    IKbSearchIndexer searchIndexer) : IKbJudgmentService
{
    private const string Container = "kb-judgments";
    private const long MaxFileSizeBytes = 50 * 1024 * 1024;

    public async Task<KbJudgmentDto> UploadAsync(Guid tenantId, Guid? actorId, UploadJudgmentInput input, byte[] fileContent, string fileName, string? mime, CancellationToken cancellationToken = default)
    {
        if (fileContent.LongLength > MaxFileSizeBytes)
        {
            throw new Application.Common.Exceptions.ValidationException([new FluentValidation.Results.ValidationFailure("file", "Judgment PDF must not exceed 50 MB (Module 12 Validation Rules).")]);
        }

        // Edge case: "duplicate judgment upload (citation match prompt merge)" — surfaced as a
        // conflict carrying the existing judgment's id, not a silent second row.
        var existing = await db.KbJudgments.SingleOrDefaultAsync(j => j.TenantId == tenantId && j.Citation == input.Citation, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException($"A judgment with citation '{input.Citation}' already exists ({existing.Id}) — merge instead of re-uploading.", "DUPLICATE_CITATION");
        }

        using var stream = new MemoryStream(fileContent);
        var scanResult = await avScanner.ScanAsync(stream, fileName, cancellationToken);
        if (!scanResult.IsClean)
        {
            throw new DomainRuleException("MALWARE_DETECTED", $"File '{fileName}' failed the antivirus scan ({scanResult.ThreatName}).", scanResult.ThreatName);
        }

        var document = new Document(tenantId, null, null, null, null, input.Citation, "Judgment", "Normal");
        await db.Documents.AddAsync(document, cancellationToken);

        var hash = Convert.ToHexString(SHA256.HashData(fileContent)).ToLowerInvariant();
        var blobPath = $"{tenantId:N}/{document.Id:N}/{fileName}";
        await blobStorage.UploadAsync(Container, blobPath, fileContent, mime ?? "application/pdf", cancellationToken);

        var version = new DocumentVersion(tenantId, document.Id, 1, blobPath, fileContent.LongLength, mime, hash, actorId);

        // Best-effort synchronous extraction (native PDF text only, per Module 7's own
        // TextExtractionService — no rasterization/OCR of scanned pages). Error Handling: "OCR
        // fail on judgment -> metadata-only searchable + retry" — a scanned PDF with no text
        // layer yields empty/whitespace text here, which is treated exactly like an OCR failure.
        var extraction = await textExtractionService.ExtractAsync(fileContent, mime ?? "application/pdf", cancellationToken);
        var extractedText = extraction.Success && !string.IsNullOrWhiteSpace(extraction.Text) ? extraction.Text : null;
        version.SetOcrStatus(extractedText is not null ? "Done" : "Failed");
        if (extractedText is not null)
        {
            version.MarkTextExtracted();
        }

        await db.DocumentVersions.AddAsync(version, cancellationToken);

        var judgment = new KbJudgment(tenantId, input.Citation, input.NeutralCitation, input.CourtId, input.DecisionDate, input.Parties, input.Headnote, document.Id);
        await db.KbJudgments.AddAsync(judgment, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await IndexAsync(tenantId, judgment, extractedText, cancellationToken);

        return ToDto(judgment, version.OcrStatus);
    }

    public async Task<KbJudgmentDto?> GetAsync(Guid tenantId, Guid judgmentId, CancellationToken cancellationToken = default)
    {
        var judgment = await db.KbJudgments.SingleOrDefaultAsync(j => j.TenantId == tenantId && j.Id == judgmentId, cancellationToken);
        return judgment is null ? null : ToDto(judgment, await GetOcrStatusAsync(judgment, cancellationToken));
    }

    public async Task<IReadOnlyList<KbJudgmentDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var judgments = await db.KbJudgments.Where(j => j.TenantId == tenantId).OrderByDescending(j => j.DecisionDate).ToListAsync(cancellationToken);
        var results = new List<KbJudgmentDto>();
        foreach (var judgment in judgments)
        {
            results.Add(ToDto(judgment, await GetOcrStatusAsync(judgment, cancellationToken)));
        }

        return results;
    }

    public async Task<KbJudgmentDto> UpdateAsync(Guid tenantId, Guid judgmentId, UpdateJudgmentInput input, CancellationToken cancellationToken = default)
    {
        var judgment = await GetJudgmentOrThrowAsync(tenantId, judgmentId, cancellationToken);
        judgment.UpdateMetadata(input.NeutralCitation, input.CourtId, input.DecisionDate, input.Parties);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(judgment, await GetOcrStatusAsync(judgment, cancellationToken));
    }

    public async Task<KbJudgmentDto> UpdateHeadnoteAsync(Guid tenantId, Guid judgmentId, string? headnote, CancellationToken cancellationToken = default)
    {
        var judgment = await GetJudgmentOrThrowAsync(tenantId, judgmentId, cancellationToken);
        judgment.UpdateHeadnote(headnote);
        await db.SaveChangesAsync(cancellationToken);

        var ocrStatus = await GetOcrStatusAsync(judgment, cancellationToken);
        var extractedText = await TryGetExtractedTextAsync(judgment, cancellationToken);
        await IndexAsync(tenantId, judgment, extractedText, cancellationToken);

        return ToDto(judgment, ocrStatus);
    }

    public async Task RetryExtractionAsync(Guid tenantId, Guid judgmentId, CancellationToken cancellationToken = default)
    {
        var judgment = await GetJudgmentOrThrowAsync(tenantId, judgmentId, cancellationToken);
        if (judgment.DocumentId is null)
        {
            return;
        }

        var version = await db.DocumentVersions.Where(v => v.TenantId == tenantId && v.DocumentId == judgment.DocumentId).OrderByDescending(v => v.VersionNo).FirstOrDefaultAsync(cancellationToken);
        if (version is null)
        {
            return;
        }

        var content = await blobStorage.DownloadAsync(Container, version.BlobPath, cancellationToken);
        var extraction = await textExtractionService.ExtractAsync(content, version.Mime, cancellationToken);
        var extractedText = extraction.Success && !string.IsNullOrWhiteSpace(extraction.Text) ? extraction.Text : null;

        version.SetOcrStatus(extractedText is not null ? "Done" : "Failed");
        if (extractedText is not null)
        {
            version.MarkTextExtracted();
        }

        await db.SaveChangesAsync(cancellationToken);
        await IndexAsync(tenantId, judgment, extractedText, cancellationToken);
    }

    public async Task<int> GetPinCountAsync(Guid tenantId, Guid judgmentId, CancellationToken cancellationToken = default)
        => await db.KbMatterPins.CountAsync(p => p.TenantId == tenantId && p.KbRefKind == "Judgment" && p.KbRefId == judgmentId, cancellationToken);

    private async Task<string> GetOcrStatusAsync(KbJudgment judgment, CancellationToken cancellationToken)
    {
        if (judgment.DocumentId is null)
        {
            return "NotApplicable";
        }

        var version = await db.DocumentVersions.Where(v => v.TenantId == judgment.TenantId && v.DocumentId == judgment.DocumentId).OrderByDescending(v => v.VersionNo).FirstOrDefaultAsync(cancellationToken);
        return version?.OcrStatus ?? "NotApplicable";
    }

    private async Task<string?> TryGetExtractedTextAsync(KbJudgment judgment, CancellationToken cancellationToken)
    {
        if (judgment.DocumentId is null)
        {
            return null;
        }

        var version = await db.DocumentVersions.Where(v => v.TenantId == judgment.TenantId && v.DocumentId == judgment.DocumentId).OrderByDescending(v => v.VersionNo).FirstOrDefaultAsync(cancellationToken);
        if (version is null || !version.TextExtracted)
        {
            return null;
        }

        var content = await blobStorage.DownloadAsync(Container, version.BlobPath, cancellationToken);
        var extraction = await textExtractionService.ExtractAsync(content, version.Mime, cancellationToken);
        return extraction.Success ? extraction.Text : null;
    }

    private async Task IndexAsync(Guid tenantId, KbJudgment judgment, string? extractedText, CancellationToken cancellationToken)
    {
        var text = string.Join(' ', new[] { judgment.Headnote, extractedText }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var tags = await db.KbItemTags.Where(t => t.TenantId == tenantId && t.KbRefKind == "Judgment" && t.KbRefId == judgment.Id).Select(t => t.TagId).ToListAsync(cancellationToken);
        var tagNames = tags.Count == 0 ? [] : await db.KbTags.Where(t => tags.Contains(t.Id)).Select(t => t.Name).ToListAsync(cancellationToken);

        await searchIndexer.IndexAsync(tenantId, new KbSearchDoc("Judgment", judgment.Id, judgment.Parties ?? judgment.Citation, text, judgment.CourtId, judgment.DecisionDate?.Year, null, tagNames, judgment.Citation), cancellationToken);
    }

    private async Task<KbJudgment> GetJudgmentOrThrowAsync(Guid tenantId, Guid judgmentId, CancellationToken cancellationToken)
        => await db.KbJudgments.SingleOrDefaultAsync(j => j.TenantId == tenantId && j.Id == judgmentId, cancellationToken)
           ?? throw new NotFoundException(nameof(KbJudgment), judgmentId);

    private static KbJudgmentDto ToDto(KbJudgment j, string ocrStatus) => new(j.Id, j.Citation, j.NeutralCitation, j.CourtId, j.DecisionDate, j.Parties, j.Headnote, j.DocumentId, ocrStatus);
}
