namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 6 external calendar sync — one implementation per provider (Google, Microsoft
/// Graph), resolved by <see cref="Provider"/> name, mirroring the ISignatureProvider
/// pattern from Module 7. Conflict rule: LexFlow is the source of truth for
/// LexFlow-born events; external edits to synced copies are overwritten on the next push
/// and noted, never merged.
/// </summary>
public interface ICalendarSync
{
    string Provider { get; }

    /// <summary>Returns the provider's OAuth2 consent-screen URL to redirect the user to.</summary>
    string GetAuthorizationUrl(Guid tenantId, Guid userId, string redirectUri);

    /// <summary>Exchanges the OAuth callback code for tokens and stores an ExternalCalendarAccount (tokens encrypted at rest).</summary>
    Task<Guid> HandleOAuthCallbackAsync(Guid tenantId, Guid userId, string code, string redirectUri, CancellationToken cancellationToken = default);

    /// <summary>Pushes a LexFlow-born event out to the external calendar, tagging it "[LexFlow]" with a deep link (outbound half of AC-CAL1).</summary>
    Task PushEventAsync(Guid tenantId, Guid externalAccountId, Guid eventId, CancellationToken cancellationToken = default);

    Task RemoveExternalEventAsync(Guid tenantId, Guid externalAccountId, Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>Incremental pull via the account's stored sync token; a 410 GONE response triggers a full resync (sync token cleared, next call re-syncs from scratch).</summary>
    Task PullChangesAsync(Guid tenantId, Guid externalAccountId, CancellationToken cancellationToken = default);

    /// <summary>removeRemoteEvents=true deletes every LexFlow-created remote event before revoking tokens (AC-CAL4).</summary>
    Task DisconnectAsync(Guid tenantId, Guid externalAccountId, bool removeRemoteEvents, CancellationToken cancellationToken = default);
}
