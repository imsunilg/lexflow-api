using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to comm.mailboxes (lexflow-database Scripts/08_Comm/Mailboxes).
/// Tokens are stored encrypted at rest (pgp_sym_encrypt via IKycEncryptionService, same
/// reuse as ExternalCalendarAccount) — the plaintext bytes never live in this entity.
/// </summary>
public sealed class Mailbox : AuditableEntity
{
    private Mailbox()
    {
    }

    public Mailbox(Guid tenantId, Guid userId, string provider, string emailAddress)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        UserId = userId;
        Provider = provider;
        EmailAddress = emailAddress;
        SyncStateJson = "{}";
        IsActive = true;
        ConnectedAt = DateTimeOffset.UtcNow;
    }

    public Guid UserId { get; private set; }
    public string Provider { get; private set; } = null!;
    public string EmailAddress { get; private set; } = null!;
    public byte[]? AccessTokenEnc { get; private set; }
    public byte[]? RefreshTokenEnc { get; private set; }
    public string SyncStateJson { get; private set; } = "{}";
    public bool IsActive { get; private set; }
    public DateTimeOffset ConnectedAt { get; private set; }
    public DateTimeOffset? DisconnectedAt { get; private set; }

    public void SetTokens(byte[]? accessTokenEnc, byte[]? refreshTokenEnc)
    {
        AccessTokenEnc = accessTokenEnc;
        RefreshTokenEnc = refreshTokenEnc;
    }

    public void SetSyncState(string syncStateJson) => SyncStateJson = syncStateJson;

    public void Disconnect()
    {
        IsActive = false;
        DisconnectedAt = DateTimeOffset.UtcNow;
    }
}
