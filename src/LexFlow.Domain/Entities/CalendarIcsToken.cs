using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to ops.calendar_ics_tokens (lexflow-database
/// Scripts/07_Ops/CalendarIcsTokens, an additive migration for this build). Module 6:
/// "ICS export per user (read-only secret URL, revocable)"; Security Rules: "ICS secret
/// 128-bit, regenerable." <see cref="Token"/> embeds the tenant id as a prefix
/// ("{tenantId:N}.{randomBase64Url}") so the public unauthenticated
/// GET /calendar/ics/{secret}.ics endpoint can resolve app.tenant_id for RLS before
/// querying — same pattern as the DMS share-link token.
/// </summary>
public sealed class CalendarIcsToken : AuditableEntity
{
    private CalendarIcsToken()
    {
    }

    public CalendarIcsToken(Guid tenantId, Guid userId, string token)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        UserId = userId;
        Token = token;
    }

    public Guid UserId { get; private set; }
    public string Token { get; private set; } = null!;
    public DateTimeOffset? RevokedAt { get; private set; }

    public void Revoke() => RevokedAt ??= DateTimeOffset.UtcNow;
}
