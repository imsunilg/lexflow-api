namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 7 external share links. Validation: expiry ≤ 30 days; hard rule: Privileged
/// documents cannot get a share link (enforced by a DB trigger — see
/// dms.document_share_links/004_Triggers.sql — this service just lets that exception
/// surface). AC-DOC5: expired link → branded 410, access attempt logged regardless.
/// </summary>
public interface IDocumentShareLinkService
{
    Task<CreateShareLinkResult> CreateAsync(Guid tenantId, Guid? actorId, Guid documentId, DateTimeOffset expiresAt, string? password, int? maxDownloads, bool watermark, CancellationToken cancellationToken = default);

    Task RevokeAsync(Guid tenantId, Guid shareLinkId, CancellationToken cancellationToken = default);

    /// <summary>Public, unauthenticated access path — resolves the raw token to its hash, validates expiry/password/download-limit, logs the attempt (IP/UA) regardless of outcome, and returns a download URL on success.</summary>
    Task<ShareLinkAccessResult> AccessAsync(string rawToken, string? password, string? ip, string? userAgent, CancellationToken cancellationToken = default);
}

public sealed record CreateShareLinkResult(Guid Id, string Token, DateTimeOffset ExpiresAt);

public sealed record ShareLinkAccessResult(bool Success, string? DownloadUrl, string? FailureReason);
