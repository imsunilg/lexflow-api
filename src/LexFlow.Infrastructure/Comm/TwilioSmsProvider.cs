using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LexFlow.Application.Common.Interfaces;

namespace LexFlow.Infrastructure.Comm;

/// <summary>Twilio SMS send (Module 11/§34). ConfigJson holds accountSid/senderId (non-secret); the auth token is the Key Vault-backed secret.</summary>
public sealed class TwilioSmsProvider(IHttpClientFactory httpClientFactory, GatewayCredentialResolver credentials) : ISmsProvider
{
    public string Provider => "sms_twilio";

    public async Task<SmsSendResult> SendAsync(Guid tenantId, string toNumber, string body, string? dltTemplateId, CancellationToken cancellationToken = default)
    {
        var resolved = await credentials.ResolveAsync(tenantId, Provider, cancellationToken);
        if (resolved is null)
        {
            return new SmsSendResult(false, null, "Failed", "Twilio is not configured for this tenant.");
        }

        var (config, authToken) = resolved.Value;
        using var configDoc = JsonDocument.Parse(config.ConfigJson);
        var accountSid = configDoc.RootElement.TryGetProperty("accountSid", out var sidProp) ? sidProp.GetString() : null;
        var senderId = configDoc.RootElement.TryGetProperty("senderId", out var senderProp) ? senderProp.GetString() : null;

        if (string.IsNullOrWhiteSpace(accountSid) || string.IsNullOrWhiteSpace(authToken))
        {
            return new SmsSendResult(false, null, "Failed", "Twilio Account SID and Auth Token are required.");
        }

        try
        {
            using var client = httpClientFactory.CreateClient();
            var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{accountSid}:{authToken}"));
            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Basic", basicAuth) },
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["To"] = toNumber,
                    ["From"] = senderId ?? string.Empty,
                    ["Body"] = body,
                }),
            };

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new SmsSendResult(false, null, "Failed", $"Twilio rejected the send: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var sid = responseBody.TryGetProperty("sid", out var sidResultProp) ? sidResultProp.GetString() : null;
            var status = responseBody.TryGetProperty("status", out var statusProp) ? statusProp.GetString() ?? "Sent" : "Sent";
            return new SmsSendResult(true, sid, MapStatus(status), null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new SmsSendResult(false, null, "Failed", ex.Message);
        }
    }

    private static string MapStatus(string twilioStatus) => twilioStatus.ToLowerInvariant() switch
    {
        "delivered" => "Delivered",
        "sent" or "queued" or "accepted" => "Sent",
        "failed" or "undelivered" => "Failed",
        _ => "Queued",
    };
}
