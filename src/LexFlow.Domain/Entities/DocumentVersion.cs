using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to dms.document_versions (lexflow-database
/// Scripts/05_DMS/DocumentVersions). Immutable once inserted (DB trigger blocks
/// updating document_id/version_no/blob_path/size_bytes/hash_sha256) — only
/// ocr_status/text_extracted are ever mutated after insert, by the text-extraction job.
/// </summary>
public sealed class DocumentVersion : AuditableEntity
{
    private DocumentVersion()
    {
    }

    public DocumentVersion(Guid tenantId, Guid documentId, int versionNo, string blobPath, long sizeBytes, string? mime, string hashSha256, Guid? uploadedBy)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        DocumentId = documentId;
        VersionNo = versionNo;
        BlobPath = blobPath;
        SizeBytes = sizeBytes;
        Mime = mime;
        HashSha256 = hashSha256;
        OcrStatus = "NotApplicable";
        TextExtracted = false;
        UploadedBy = uploadedBy;
    }

    public Guid DocumentId { get; private set; }
    public int VersionNo { get; private set; }
    public string BlobPath { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public string? Mime { get; private set; }
    public string HashSha256 { get; private set; } = null!;
    public string OcrStatus { get; private set; } = "NotApplicable";
    public bool TextExtracted { get; private set; }
    public Guid? UploadedBy { get; private set; }

    public void SetOcrStatus(string status) => OcrStatus = status;

    public void MarkTextExtracted() => TextExtracted = true;
}
