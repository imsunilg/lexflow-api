using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to dms.document_share_links (lexflow-database
/// Scripts/05_DMS/DocumentShareLinks). Hard rule (DB trigger backstop): Privileged
/// documents cannot get a share link at all. token_hash never stores the raw token —
/// same write-only-secret pattern as refresh tokens (only the hash is persisted;
/// the raw token is returned once, at creation).
/// </summary>
public sealed class DocumentShareLink : AuditableEntity
{
    private DocumentShareLink()
    {
    }

    public DocumentShareLink(Guid tenantId, Guid documentId, string tokenHash, DateTimeOffset expiresAt, string? passwordHash, int? maxDownloads, bool watermark)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        DocumentId = documentId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        PasswordHash = passwordHash;
        MaxDownloads = maxDownloads;
        Downloads = 0;
        Watermark = watermark;
    }

    public Guid DocumentId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public string? PasswordHash { get; private set; }
    public int? MaxDownloads { get; private set; }
    public int Downloads { get; private set; }
    public bool Watermark { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public void RecordDownload() => Downloads++;

    public void Revoke() => RevokedAt = DateTimeOffset.UtcNow;

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now && (MaxDownloads is null || Downloads < MaxDownloads);
}
