namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 7 pipeline: "extract text (native PDF text or OCR via Tesseract worker for
/// scans/images)". Invoked from a Hangfire background job after upload — never
/// inline in the request (AC-DOC1 gives this up to 60s for a 10-page scan, far longer
/// than an HTTP request should block for).
/// </summary>
public interface ITextExtractionService
{
    /// <summary>Extracts text from the given blob content and returns it; the caller is responsible for persisting/indexing it (see DocumentProcessingJobs).</summary>
    Task<TextExtractionResult> ExtractAsync(byte[] content, string? mime, CancellationToken cancellationToken = default);
}

public sealed record TextExtractionResult(bool Success, string? Text, string OcrStatus);
