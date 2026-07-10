using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LexFlow.Application.Common.Interfaces;

namespace LexFlow.Infrastructure.Comm;

/// <summary>MSG91 SMS send — India DLT-compliant gateway (Module 11/§34). ConfigJson holds senderId (non-secret); the auth key is the Key Vault-backed secret.</summary>
public sealed class Msg91SmsProvider(IHttpClientFactory httpClientFactory, GatewayCredentialResolver credentials) : ISmsProvider
{
    public string Provider => "sms_msg91";

    public async Task<SmsSendResult> SendAsync(Guid tenantId, string toNumber, string body, string? dltTemplateId, CancellationToken cancellationToken = default)
    {
        var resolved = await credentials.ResolveAsync(tenantId, Provider, cancellationToken);
        if (resolved is null)
        {
            return new SmsSendResult(false, null, "Failed", "MSG91 is not configured for this tenant.");
        }

        var (config, authKey) = resolved.Value;
        using var configDoc = JsonDocument.Parse(config.ConfigJson);
        var senderId = configDoc.RootElement.TryGetProperty("senderId", out var senderProp) ? senderProp.GetString() : null;

        if (string.IsNullOrWhiteSpace(authKey))
        {
            return new SmsSendResult(false, null, "Failed", "MSG91 auth key is required.");
        }

        try
        {
            using var client = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://control.msg91.com/api/v5/flow/")
            {
                Headers = { { "authkey", authKey } },
                Content = new StringContent(
                    JsonSerializer.Serialize(new { sender = senderId, mobiles = toNumber, DLT_TE_ID = dltTemplateId, message = body }),
                    Encoding.UTF8,
                    "application/json"),
            };

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new SmsSendResult(false, null, "Failed", $"MSG91 rejected the send: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var requestId = responseBody.TryGetProperty("requestId", out var idProp) ? idProp.GetString() : null;
            return new SmsSendResult(true, requestId, "Sent", null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new SmsSendResult(false, null, "Failed", ex.Message);
        }
    }
}
