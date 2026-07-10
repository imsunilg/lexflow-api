using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexFlow.Infrastructure.Ops;

/// <summary>
/// Module 6: webhook push notifications from Google/Microsoft Graph are the primary sync
/// path (AC-CAL1: LexFlow-born event appears externally within 60s, and vice versa via
/// webhook). This job is the polling fallback for the case a webhook is missed, delayed, or
/// the subscription/watch has silently expired — it walks every connected
/// ExternalCalendarAccount and calls the matching ICalendarSync.PullChangesAsync, which
/// itself handles incremental-vs-full resync via the account's stored sync token (a 410 GONE
/// from the provider clears the token and forces a full resync on the next call). One
/// account's failure (revoked token, transient network error) must never block the rest of
/// the batch, so each account is isolated in its own try/catch.
/// </summary>
public sealed class CalendarSyncPollingJob(LexFlowDbContext db, IEnumerable<ICalendarSync> calendarSyncProviders, ILogger<CalendarSyncPollingJob> logger)
{
    public async Task PollAsync(CancellationToken cancellationToken = default)
    {
        var providers = calendarSyncProviders.ToDictionary(p => p.Provider, StringComparer.OrdinalIgnoreCase);

        var accounts = await db.ExternalCalendarAccounts
            .Where(a => a.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var account in accounts)
        {
            if (!providers.TryGetValue(account.Provider, out var sync))
            {
                logger.LogWarning("Calendar sync poll: no ICalendarSync registered for provider {Provider} (account {AccountId}).", account.Provider, account.Id);
                continue;
            }

            try
            {
                await sync.PullChangesAsync(account.TenantId, account.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Calendar sync poll failed for account {AccountId} ({Provider}).", account.Id, account.Provider);
            }
        }
    }
}
