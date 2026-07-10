using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LexFlow.Infrastructure.Comm;

/// <summary>Microsoft Graph (Office 365) two-way mailbox sync (Module 11) — see GmailSyncService for the shared simplification note (no live sandbox account).</summary>
public sealed class MicrosoftGraphEmailSyncService(IHttpClientFactory httpClientFactory, IOptions<MicrosoftGraphEmailOptions> options, IKycEncryptionService encryption, IInboundEmailHandler emailService, LexFlowDbContext db) : IEmailSync
{
    public string Provider => "microsoft365";

    public string GetAuthorizationUrl(Guid tenantId, Guid userId, string redirectUri)
    {
        var opts = options.Value;
        var query = System.Web.HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = opts.ClientId;
        query["redirect_uri"] = redirectUri;
        query["response_type"] = "code";
        query["response_mode"] = "query";
        query["scope"] = "offline_access Mail.Read Mail.Send";
        query["state"] = $"{tenantId:N}:{userId:N}";
        return $"{opts.AuthorizeUrl}?{query}";
    }

    public async Task<Guid> HandleOAuthCallbackAsync(Guid tenantId, Guid userId, string code, string redirectUri, CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        using var client = httpClientFactory.CreateClient();

        using var tokenResponse = await client.PostAsync(opts.TokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = opts.ClientId,
            ["client_secret"] = opts.ClientSecret,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code",
        }), cancellationToken);
        tokenResponse.EnsureSuccessStatusCode();
        var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var accessToken = tokenBody.GetProperty("access_token").GetString()!;
        var refreshToken = tokenBody.TryGetProperty("refresh_token", out var refreshProp) ? refreshProp.GetString() : null;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var meResponse = await client.GetAsync($"{opts.ApiBaseUrl}/me", cancellationToken);
        var emailAddress = "unknown";
        if (meResponse.IsSuccessStatusCode)
        {
            var me = await meResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            emailAddress = me.TryGetProperty("mail", out var mailProp) ? mailProp.GetString() ?? "unknown" : me.TryGetProperty("userPrincipalName", out var upnProp) ? upnProp.GetString() ?? "unknown" : "unknown";
        }

        var mailbox = new Mailbox(tenantId, userId, Provider, emailAddress);
        mailbox.SetTokens(
            await encryption.EncryptAsync(accessToken, cancellationToken),
            refreshToken is null ? null : await encryption.EncryptAsync(refreshToken, cancellationToken));

        await db.Mailboxes.AddAsync(mailbox, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return mailbox.Id;
    }

    public async Task PullChangesAsync(Guid tenantId, Guid mailboxId, CancellationToken cancellationToken = default)
    {
        var mailbox = await GetMailboxOrThrowAsync(tenantId, mailboxId, cancellationToken);
        using var client = await CreateAuthorizedClientAsync(mailbox, cancellationToken);
        var opts = options.Value;

        using var response = await client.GetAsync($"{opts.ApiBaseUrl}/me/messages?$top=25&$select=internetMessageId,from,toRecipients,subject,receivedDateTime", cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        if (!body.TryGetProperty("value", out var messages))
        {
            return;
        }

        foreach (var message in messages.EnumerateArray())
        {
            var fromAddr = message.TryGetProperty("from", out var fromProp) ? fromProp.GetProperty("emailAddress").GetProperty("address").GetString() ?? string.Empty : string.Empty;
            var toAddrs = message.TryGetProperty("toRecipients", out var toProp)
                ? toProp.EnumerateArray().Select(r => r.GetProperty("emailAddress").GetProperty("address").GetString() ?? string.Empty).ToList()
                : [];
            var subject = message.TryGetProperty("subject", out var subjectProp) ? subjectProp.GetString() : null;
            var messageIdHdr = message.TryGetProperty("internetMessageId", out var midProp) ? midProp.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
            var receivedAt = message.TryGetProperty("receivedDateTime", out var dateProp) && DateTimeOffset.TryParse(dateProp.GetString(), out var parsed) ? parsed : DateTimeOffset.UtcNow;

            await emailService.HandleInboundAsync(tenantId, new InboundEmailInput(messageIdHdr, null, fromAddr, toAddrs, subject, string.Empty, receivedAt, null), cancellationToken);
        }

        mailbox.SetSyncState(JsonSerializer.Serialize(new { lastSyncedAt = DateTimeOffset.UtcNow }));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> SendAsync(Guid tenantId, Guid mailboxId, EmailSendRequest request, CancellationToken cancellationToken = default)
    {
        var mailbox = await GetMailboxOrThrowAsync(tenantId, mailboxId, cancellationToken);
        using var client = await CreateAuthorizedClientAsync(mailbox, cancellationToken);
        var opts = options.Value;

        var graphMessage = new
        {
            message = new
            {
                subject = request.Subject,
                body = new { contentType = "HTML", content = request.BodyHtml },
                toRecipients = request.ToAddresses.Select(a => new { emailAddress = new { address = a } }).ToArray(),
            },
            saveToSentItems = true,
        };

        using var response = await client.PostAsJsonAsync($"{opts.ApiBaseUrl}/me/sendMail", graphMessage, cancellationToken);
        response.EnsureSuccessStatusCode();

        // Graph's sendMail returns 202 Accepted with no body/id; the caller (IEmailService)
        // generates its own message_id_hdr for the local record in this case.
        return Guid.NewGuid().ToString();
    }

    public async Task DisconnectAsync(Guid tenantId, Guid mailboxId, CancellationToken cancellationToken = default)
    {
        var mailbox = await GetMailboxOrThrowAsync(tenantId, mailboxId, cancellationToken);
        mailbox.SetTokens(null, null);
        mailbox.Disconnect();
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Mailbox> GetMailboxOrThrowAsync(Guid tenantId, Guid mailboxId, CancellationToken cancellationToken)
        => await db.Mailboxes.SingleOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, cancellationToken)
            ?? throw new NotFoundException(nameof(Mailbox), mailboxId);

    private async Task<HttpClient> CreateAuthorizedClientAsync(Mailbox mailbox, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        if (mailbox.AccessTokenEnc is { Length: > 0 })
        {
            var accessToken = await encryption.DecryptAsync(mailbox.AccessTokenEnc, cancellationToken);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client;
    }
}
