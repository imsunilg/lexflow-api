using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to ops.external_calendar_accounts (lexflow-database
/// Scripts/07_Ops/ExternalCalendarAccounts). Tokens are stored encrypted at rest
/// (pgp_sym_encrypt via the same KycEncryptionService-style helper) — the plaintext
/// bytes never live in this entity, only the already-encrypted bytea payload.
/// </summary>
public sealed class ExternalCalendarAccount : AuditableEntity
{
    private ExternalCalendarAccount()
    {
    }

    public ExternalCalendarAccount(Guid tenantId, Guid userId, string provider, string? accountEmail)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        UserId = userId;
        Provider = provider;
        AccountEmail = accountEmail;
        IsActive = true;
        ConnectedAt = DateTimeOffset.UtcNow;
    }

    public Guid UserId { get; private set; }
    public string Provider { get; private set; } = null!;
    public string? AccountEmail { get; private set; }
    public byte[]? AccessTokenEnc { get; private set; }
    public byte[]? RefreshTokenEnc { get; private set; }
    public string? SyncToken { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset ConnectedAt { get; private set; }
    public DateTimeOffset? DisconnectedAt { get; private set; }

    public void SetTokens(byte[]? accessTokenEnc, byte[]? refreshTokenEnc)
    {
        AccessTokenEnc = accessTokenEnc;
        RefreshTokenEnc = refreshTokenEnc;
    }

    public void SetSyncToken(string? syncToken) => SyncToken = syncToken;

    public void Disconnect()
    {
        IsActive = false;
        DisconnectedAt = DateTimeOffset.UtcNow;
    }
}
