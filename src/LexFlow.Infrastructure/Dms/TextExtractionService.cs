using LexFlow.Application.Common.Interfaces;
using Tesseract;
using UglyToad.PdfPig;

namespace LexFlow.Infrastructure.Dms;

/// <summary>
/// Module 7 pipeline: "extract text (native PDF text via PdfPig or OCR via Tesseract
/// worker for scans/images)". Runs inside a Hangfire background job
/// (DocumentProcessingJobs.ExtractTextAsync), never inline in the upload request.
/// </summary>
public sealed class TextExtractionService(TesseractOptions tesseractOptions) : ITextExtractionService
{
    private static readonly HashSet<string> ImageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/tiff",
    };

    public Task<TextExtractionResult> ExtractAsync(byte[] content, string? mime, CancellationToken cancellationToken = default)
        => Task.FromResult(ExtractCore(content, mime));

    private TextExtractionResult ExtractCore(byte[] content, string? mime)
    {
        try
        {
            if (mime == "application/pdf")
            {
                return ExtractNativePdfText(content);
            }

            if (mime is not null && ImageMimeTypes.Contains(mime))
            {
                return ExtractViaOcr(content);
            }

            // Office/other formats: full extraction is out of scope here (would need a
            // LibreOffice-style conversion worker, per Module 7's own preview pipeline) —
            // metadata-only search still works per Error Handling: "OCR failure → status
            // OcrFailed, searchable by metadata only, retry button."
            return new TextExtractionResult(true, null, "NotApplicable");
        }
        catch (Exception)
        {
            return new TextExtractionResult(false, null, "Failed");
        }
    }

    private static TextExtractionResult ExtractNativePdfText(byte[] content)
    {
        using var document = PdfDocument.Open(content);
        var text = string.Join("\n", document.GetPages().Select(p => p.Text));
        return new TextExtractionResult(true, text, "Done");
    }

    private TextExtractionResult ExtractViaOcr(byte[] content)
    {
        using var engine = new TesseractEngine(tesseractOptions.TessDataPath, "eng", EngineMode.Default);
        using var img = Pix.LoadFromMemory(content);
        using var page = engine.Process(img);
        var text = page.GetText();
        return new TextExtractionResult(true, text, "Done");
    }
}

/// <summary>Bound from configuration section "Tesseract". TessDataPath must point at a directory containing the .traineddata language files (e.g. eng.traineddata).</summary>
public sealed class TesseractOptions
{
    public const string SectionName = "Tesseract";

    public string TessDataPath { get; set; } = "./tessdata";
}
