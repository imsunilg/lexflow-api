using System.Security.Cryptography;
using System.Text;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Dms;

/// <summary>
/// Module 7 external share links. AC-DOC5: expired/revoked/exhausted links fail with a
/// logged access attempt regardless of outcome (Security Rules: "share-link access logs
/// IP+UA"). Privileged documents are blocked from getting a link at all by a DB trigger
/// (dms.document_share_links/004_Triggers.sql) — this service adds the same check at
/// the application layer first, purely so the caller gets a clean ConflictException
/// instead of a raw Postgres constraint-violation exception.
/// </summary>
public sealed class DocumentShareLinkService(LexFlowDbContext db, IPasswordHasher passwordHasher, IBlobStorageService blobStorage) : IDocumentShareLinkService
{
    private const string Container = "documents";

    public async Task<CreateShareLinkResult> CreateAsync(Guid tenantId, Guid? actorId, Guid documentId, DateTimeOffset expiresAt, string? password, int? maxDownloads, bool watermark, CancellationToken cancellationToken = default)
    {
        var document = await db.Documents.SingleOrDefaultAsync(d => d.TenantId == tenantId && d.Id == documentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Document), documentId);

        if (document.Confidentiality == "Privileged")
        {
            throw new ConflictException("Privileged documents cannot get an external share link (Module 7 hard rule).", "PRIVILEGED_NO_SHARE_LINK");
        }

        if (expiresAt > DateTimeOffset.UtcNow.AddDays(30))
        {
            throw new Application.Common.Exceptions.ValidationException([new FluentValidation.Results.ValidationFailure("expiresAt", "Share link expiry cannot exceed 30 days.")]);
        }

        // The token embeds the tenant id as a plain prefix ("{tenantId:N}.{random}"). This
        // public, unauthenticated endpoint has no JWT to source a tenant claim from and
        // dms.document_share_links has RLS enabled (current_setting('app.tenant_id') with no
        // default — it errors rather than returning zero rows if unset), so the token itself
        // must carry enough to call SET LOCAL app.tenant_id before any query runs. Same
        // pattern as the web-to-lead capture endpoint's formKey. The full token (prefix
        // included) is still what gets hashed and compared, so knowing the tenant id alone
        // never helps guess a valid token.
        var randomPart = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var rawToken = $"{tenantId:N}.{randomPart}";
        var tokenHash = HashToken(rawToken);
        var passwordHash = string.IsNullOrEmpty(password) ? null : passwordHasher.Hash(password);

        var shareLink = new DocumentShareLink(tenantId, documentId, tokenHash, expiresAt, passwordHash, maxDownloads, watermark);
        await db.DocumentShareLinks.AddAsync(shareLink, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateShareLinkResult(shareLink.Id, rawToken, expiresAt);
    }

    public async Task RevokeAsync(Guid tenantId, Guid shareLinkId, CancellationToken cancellationToken = default)
    {
        var shareLink = await db.DocumentShareLinks.SingleOrDefaultAsync(s => s.TenantId == tenantId && s.Id == shareLinkId, cancellationToken)
            ?? throw new NotFoundException(nameof(DocumentShareLink), shareLinkId);

        shareLink.Revoke();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ShareLinkAccessResult> AccessAsync(string rawToken, string? password, string? ip, string? userAgent, CancellationToken cancellationToken = default)
    {
        var separatorIndex = rawToken.IndexOf('.');
        if (separatorIndex <= 0 || !Guid.TryParseExact(rawToken[..separatorIndex], "N", out var tenantId))
        {
            return new ShareLinkAccessResult(false, null, "NOT_FOUND");
        }

        await db.SetTenantIdAsync(tenantId, cancellationToken);

        var tokenHash = HashToken(rawToken);
        var shareLink = await db.DocumentShareLinks.SingleOrDefaultAsync(s => s.TokenHash == tokenHash, cancellationToken);

        if (shareLink is null)
        {
            return new ShareLinkAccessResult(false, null, "NOT_FOUND");
        }

        // AC-DOC5: "access attempt logged" — logged for every attempt, success or failure.
        await db.DocumentActivities.AddAsync(new DocumentActivity(shareLink.TenantId, shareLink.DocumentId, null, "View", ip, userAgent), cancellationToken);

        if (!shareLink.IsActive(DateTimeOffset.UtcNow))
        {
            await db.SaveChangesAsync(cancellationToken);
            return new ShareLinkAccessResult(false, null, "EXPIRED_OR_EXHAUSTED");
        }

        if (shareLink.PasswordHash is not null && (password is null || !passwordHasher.Verify(password, shareLink.PasswordHash)))
        {
            await db.SaveChangesAsync(cancellationToken);
            return new ShareLinkAccessResult(false, null, "PASSWORD_REQUIRED_OR_INVALID");
        }

        var document = await db.Documents.SingleAsync(d => d.Id == shareLink.DocumentId, cancellationToken);
        if (document.CurrentVersionId is null)
        {
            await db.SaveChangesAsync(cancellationToken);
            return new ShareLinkAccessResult(false, null, "NO_CURRENT_VERSION");
        }

        var version = await db.DocumentVersions.SingleAsync(v => v.Id == document.CurrentVersionId, cancellationToken);
        var url = await blobStorage.GetDownloadUrlAsync(Container, version.BlobPath, TimeSpan.FromMinutes(15), cancellationToken);

        shareLink.RecordDownload();
        await db.DocumentActivities.AddAsync(new DocumentActivity(shareLink.TenantId, shareLink.DocumentId, null, "Download", ip, userAgent), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return new ShareLinkAccessResult(true, url, null);
    }

    private static string HashToken(string rawToken) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
}
