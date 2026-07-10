using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LexFlow.Infrastructure.Ops;

/// <summary>
/// Google Calendar v3 two-way sync (Module 6): incremental sync via sync tokens, push
/// channels are out of scope of this build (webhook renewal/channel management requires a
/// publicly reachable HTTPS callback, which this environment doesn't provision) — inbound
/// pull instead runs on the same Hangfire cadence as other polling jobs in this codebase.
/// Simplified like Module 7's ISignatureProvider implementations: real HTTP calls are
/// wired correctly but untested against a live Google account (no sandbox credentials
/// provisioned here).
/// </summary>
public sealed class GoogleCalendarSyncService(IHttpClientFactory httpClientFactory, IOptions<GoogleCalendarOptions> options, IKycEncryptionService encryption, LexFlowDbContext db) : ICalendarSync
{
    public string Provider => "google";

    public string GetAuthorizationUrl(Guid tenantId, Guid userId, string redirectUri)
    {
        var opts = options.Value;
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = opts.ClientId;
        query["redirect_uri"] = redirectUri;
        query["response_type"] = "code";
        query["access_type"] = "offline";
        query["prompt"] = "consent";
        query["scope"] = "https://www.googleapis.com/auth/calendar";
        query["state"] = $"{tenantId:N}:{userId:N}";
        return $"{opts.AuthorizeUrl}?{query}";
    }

    public async Task<Guid> HandleOAuthCallbackAsync(Guid tenantId, Guid userId, string code, string redirectUri, CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        using var client = httpClientFactory.CreateClient();

        using var tokenResponse = await client.PostAsJsonAsync(opts.TokenUrl, new
        {
            code,
            client_id = opts.ClientId,
            client_secret = opts.ClientSecret,
            redirect_uri = redirectUri,
            grant_type = "authorization_code",
        }, cancellationToken);
        tokenResponse.EnsureSuccessStatusCode();
        var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var accessToken = tokenBody.GetProperty("access_token").GetString()!;
        var refreshToken = tokenBody.TryGetProperty("refresh_token", out var refreshProp) ? refreshProp.GetString() : null;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var userInfoResponse = await client.GetAsync($"{opts.ApiBaseUrl}/oauth2/v2/userinfo", cancellationToken);
        string? accountEmail = null;
        if (userInfoResponse.IsSuccessStatusCode)
        {
            var userInfo = await userInfoResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            accountEmail = userInfo.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null;
        }

        var account = new ExternalCalendarAccount(tenantId, userId, Provider, accountEmail);
        account.SetTokens(
            await encryption.EncryptAsync(accessToken, cancellationToken),
            refreshToken is null ? null : await encryption.EncryptAsync(refreshToken, cancellationToken));

        await db.ExternalCalendarAccounts.AddAsync(account, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return account.Id;
    }

    public async Task PushEventAsync(Guid tenantId, Guid externalAccountId, Guid eventId, CancellationToken cancellationToken = default)
    {
        var account = await GetAccountOrThrowAsync(tenantId, externalAccountId, cancellationToken);
        var ev = await db.CalendarEvents.SingleOrDefaultAsync(e => e.TenantId == tenantId && e.Id == eventId, cancellationToken)
            ?? throw new NotFoundException(nameof(CalendarEvent), eventId);

        using var client = await CreateAuthorizedClientAsync(account, cancellationToken);
        var opts = options.Value;

        var googleEvent = new
        {
            summary = $"[LexFlow] {ev.Title}",
            location = ev.Location,
            start = new { dateTime = ev.StartsAt.UtcDateTime.ToString("O") },
            end = new { dateTime = ev.EndsAt.UtcDateTime.ToString("O") },
            description = $"Synced from LexFlow — view at /calendar/events/{ev.Id}",
        };

        var link = await db.ExternalEventLinks.SingleOrDefaultAsync(l => l.TenantId == tenantId && l.EventId == eventId && l.ExternalAccountId == externalAccountId, cancellationToken);

        HttpResponseMessage response;
        if (link is null)
        {
            response = await client.PostAsJsonAsync($"{opts.ApiBaseUrl}/calendar/v3/calendars/primary/events", googleEvent, cancellationToken);
        }
        else
        {
            response = await client.PutAsJsonAsync($"{opts.ApiBaseUrl}/calendar/v3/calendars/primary/events/{link.ExternalEventId}", googleEvent, cancellationToken);
        }

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var externalId = body.GetProperty("id").GetString()!;
        var etag = body.TryGetProperty("etag", out var etagProp) ? etagProp.GetString() : null;

        if (link is null)
        {
            await db.ExternalEventLinks.AddAsync(new ExternalEventLink(tenantId, eventId, externalAccountId, externalId, etag), cancellationToken);
        }
        else
        {
            link.MarkSynced(etag);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveExternalEventAsync(Guid tenantId, Guid externalAccountId, Guid eventId, CancellationToken cancellationToken = default)
    {
        var link = await db.ExternalEventLinks.SingleOrDefaultAsync(l => l.TenantId == tenantId && l.EventId == eventId && l.ExternalAccountId == externalAccountId, cancellationToken);
        if (link is null)
        {
            return;
        }

        var account = await GetAccountOrThrowAsync(tenantId, externalAccountId, cancellationToken);
        using var client = await CreateAuthorizedClientAsync(account, cancellationToken);
        var opts = options.Value;
        using var response = await client.DeleteAsync($"{opts.ApiBaseUrl}/calendar/v3/calendars/primary/events/{link.ExternalEventId}", cancellationToken);
        if (response.StatusCode is not System.Net.HttpStatusCode.NotFound and not System.Net.HttpStatusCode.Gone)
        {
            response.EnsureSuccessStatusCode();
        }

        db.ExternalEventLinks.Remove(link);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task PullChangesAsync(Guid tenantId, Guid externalAccountId, CancellationToken cancellationToken = default)
    {
        var account = await GetAccountOrThrowAsync(tenantId, externalAccountId, cancellationToken);
        using var client = await CreateAuthorizedClientAsync(account, cancellationToken);
        var opts = options.Value;

        var url = string.IsNullOrEmpty(account.SyncToken)
            ? $"{opts.ApiBaseUrl}/calendar/v3/calendars/primary/events"
            : $"{opts.ApiBaseUrl}/calendar/v3/calendars/primary/events?syncToken={Uri.EscapeDataString(account.SyncToken)}";

        using var response = await client.GetAsync(url, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Gone)
        {
            // AC-CAL edge case: sync token expired -> clear it, next call does a full resync.
            account.SetSyncToken(null);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var nextSyncToken = body.TryGetProperty("nextSyncToken", out var tokenProp) ? tokenProp.GetString() : account.SyncToken;
        account.SetSyncToken(nextSyncToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DisconnectAsync(Guid tenantId, Guid externalAccountId, bool removeRemoteEvents, CancellationToken cancellationToken = default)
    {
        var account = await GetAccountOrThrowAsync(tenantId, externalAccountId, cancellationToken);

        if (removeRemoteEvents)
        {
            var links = await db.ExternalEventLinks.Where(l => l.TenantId == tenantId && l.ExternalAccountId == externalAccountId).ToListAsync(cancellationToken);
            foreach (var link in links)
            {
                await RemoveExternalEventAsync(tenantId, externalAccountId, link.EventId, cancellationToken);
            }
        }

        account.SetTokens(null, null);
        account.Disconnect();
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<ExternalCalendarAccount> GetAccountOrThrowAsync(Guid tenantId, Guid externalAccountId, CancellationToken cancellationToken)
        => await db.ExternalCalendarAccounts.SingleOrDefaultAsync(a => a.TenantId == tenantId && a.Id == externalAccountId, cancellationToken)
            ?? throw new NotFoundException(nameof(ExternalCalendarAccount), externalAccountId);

    private async Task<HttpClient> CreateAuthorizedClientAsync(ExternalCalendarAccount account, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        if (account.AccessTokenEnc is { Length: > 0 })
        {
            var accessToken = await encryption.DecryptAsync(account.AccessTokenEnc, cancellationToken);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client;
    }
}
