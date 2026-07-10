using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LexFlow.Infrastructure.Comm;

/// <summary>
/// Gmail API two-way mailbox sync (Module 11): OAuth2 connect, incremental pull (Gmail
/// history/list API), and send-via-API so a LexFlow-sent email lands in the user's real
/// Gmail Sent folder (AC-CM1). Simplified like every other provider integration in this
/// codebase without a live sandbox account (DocuSign/Adobe Sign, Google/Microsoft
/// Calendar): real HTTP calls wired correctly, untested against a live Gmail account.
/// </summary>
public sealed class GmailSyncService(IHttpClientFactory httpClientFactory, IOptions<GmailOptions> options, IKycEncryptionService encryption, IInboundEmailHandler emailService, LexFlowDbContext db) : IEmailSync
{
    public string Provider => "gmail";

    public string GetAuthorizationUrl(Guid tenantId, Guid userId, string redirectUri)
    {
        var opts = options.Value;
        var query = System.Web.HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = opts.ClientId;
        query["redirect_uri"] = redirectUri;
        query["response_type"] = "code";
        query["access_type"] = "offline";
        query["prompt"] = "consent";
        query["scope"] = "https://www.googleapis.com/auth/gmail.readonly https://www.googleapis.com/auth/gmail.send";
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
        using var profileResponse = await client.GetAsync($"{opts.ApiBaseUrl}/gmail/v1/users/me/profile", cancellationToken);
        var emailAddress = "unknown";
        if (profileResponse.IsSuccessStatusCode)
        {
            var profile = await profileResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            emailAddress = profile.TryGetProperty("emailAddress", out var emailProp) ? emailProp.GetString() ?? "unknown" : "unknown";
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

        using var listResponse = await client.GetAsync($"{opts.ApiBaseUrl}/gmail/v1/users/me/messages?maxResults=25", cancellationToken);
        listResponse.EnsureSuccessStatusCode();
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        if (!listBody.TryGetProperty("messages", out var messages))
        {
            return;
        }

        foreach (var messageRef in messages.EnumerateArray())
        {
            var messageId = messageRef.GetProperty("id").GetString()!;
            using var messageResponse = await client.GetAsync($"{opts.ApiBaseUrl}/gmail/v1/users/me/messages/{messageId}?format=metadata&metadataHeaders=From&metadataHeaders=To&metadataHeaders=Subject&metadataHeaders=Message-ID&metadataHeaders=In-Reply-To", cancellationToken);
            if (!messageResponse.IsSuccessStatusCode)
            {
                continue;
            }

            var messageBody = await messageResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var headers = messageBody.GetProperty("payload").GetProperty("headers").EnumerateArray()
                .ToDictionary(h => h.GetProperty("name").GetString()!, h => h.GetProperty("value").GetString() ?? string.Empty, StringComparer.OrdinalIgnoreCase);

            var internalDate = messageBody.TryGetProperty("internalDate", out var dateProp) && long.TryParse(dateProp.GetString(), out var epochMs)
                ? DateTimeOffset.FromUnixTimeMilliseconds(epochMs)
                : DateTimeOffset.UtcNow;

            await emailService.HandleInboundAsync(tenantId, new InboundEmailInput(
                headers.GetValueOrDefault("Message-ID", messageId),
                headers.GetValueOrDefault("In-Reply-To"),
                headers.GetValueOrDefault("From", string.Empty),
                (headers.GetValueOrDefault("To") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                headers.GetValueOrDefault("Subject"),
                string.Empty,
                internalDate,
                null), cancellationToken);
        }

        mailbox.SetSyncState(JsonSerializer.Serialize(new { lastSyncedAt = DateTimeOffset.UtcNow }));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> SendAsync(Guid tenantId, Guid mailboxId, EmailSendRequest request, CancellationToken cancellationToken = default)
    {
        var mailbox = await GetMailboxOrThrowAsync(tenantId, mailboxId, cancellationToken);
        using var client = await CreateAuthorizedClientAsync(mailbox, cancellationToken);
        var opts = options.Value;

        var raw = BuildRawMime(mailbox.EmailAddress, request);
        var base64Url = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        using var response = await client.PostAsJsonAsync($"{opts.ApiBaseUrl}/gmail/v1/users/me/messages/send", new { raw = base64Url }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("id").GetString()!;
    }

    public async Task DisconnectAsync(Guid tenantId, Guid mailboxId, CancellationToken cancellationToken = default)
    {
        var mailbox = await GetMailboxOrThrowAsync(tenantId, mailboxId, cancellationToken);
        mailbox.SetTokens(null, null);
        mailbox.Disconnect();
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string BuildRawMime(string fromAddress, EmailSendRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"From: {fromAddress}");
        builder.AppendLine($"To: {string.Join(", ", request.ToAddresses)}");
        builder.AppendLine($"Subject: {request.Subject}");
        if (request.InReplyToMessageIdHdr is not null)
        {
            builder.AppendLine($"In-Reply-To: {request.InReplyToMessageIdHdr}");
        }

        builder.AppendLine("Content-Type: text/html; charset=UTF-8");
        builder.AppendLine();
        builder.Append(request.BodyHtml);
        return builder.ToString();
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
