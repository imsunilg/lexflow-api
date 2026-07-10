using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LexFlow.Application.Common.Interfaces;

namespace LexFlow.Infrastructure.Comm;

/// <summary>WhatsApp Business Cloud API send (Module 11/§34). ConfigJson holds wabaId/phoneNumberId (non-secret); the access token is the Key Vault-backed secret.</summary>
public sealed class WhatsAppCloudApiProvider(IHttpClientFactory httpClientFactory, GatewayCredentialResolver credentials) : IWhatsAppProvider
{
    private const string Provider = "whatsapp";
    private const string ApiBaseUrl = "https://graph.facebook.com/v19.0";

    public Task<WhatsAppSendResult> SendSessionMessageAsync(Guid tenantId, string toPhoneE164, string body, CancellationToken cancellationToken = default)
        => SendAsync(tenantId, toPhoneE164, new { messaging_product = "whatsapp", to = toPhoneE164, type = "text", text = new { body } }, cancellationToken);

    public Task<WhatsAppSendResult> SendTemplateMessageAsync(Guid tenantId, string toPhoneE164, string hsmName, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken = default)
        => SendAsync(tenantId, toPhoneE164, new
        {
            messaging_product = "whatsapp",
            to = toPhoneE164,
            type = "template",
            template = new
            {
                name = hsmName,
                language = new { code = "en" },
                components = new[]
                {
                    new
                    {
                        type = "body",
                        parameters = variables.Select(v => new { type = "text", text = v.Value }).ToArray(),
                    },
                },
            },
        }, cancellationToken);

    private async Task<WhatsAppSendResult> SendAsync(Guid tenantId, string toPhoneE164, object payload, CancellationToken cancellationToken)
    {
        var resolved = await credentials.ResolveAsync(tenantId, Provider, cancellationToken);
        if (resolved is null)
        {
            return new WhatsAppSendResult(false, null, "Failed", "WhatsApp Cloud API is not configured for this tenant.");
        }

        var (config, accessToken) = resolved.Value;
        using var configDoc = JsonDocument.Parse(config.ConfigJson);
        var phoneNumberId = configDoc.RootElement.TryGetProperty("phoneNumberId", out var phoneProp) ? phoneProp.GetString() : null;

        if (string.IsNullOrWhiteSpace(phoneNumberId) || string.IsNullOrWhiteSpace(accessToken))
        {
            return new WhatsAppSendResult(false, null, "Failed", "WhatsApp phoneNumberId and access token are required.");
        }

        try
        {
            using var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await client.PostAsync(
                $"{ApiBaseUrl}/{phoneNumberId}/messages",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new WhatsAppSendResult(false, null, "Failed", $"WhatsApp Cloud API rejected the send: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var waMsgId = body.TryGetProperty("messages", out var messagesProp) && messagesProp.GetArrayLength() > 0
                ? messagesProp[0].GetProperty("id").GetString()
                : Guid.NewGuid().ToString();

            return new WhatsAppSendResult(true, waMsgId, "Sent", null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new WhatsAppSendResult(false, null, "Failed", ex.Message);
        }
    }
}
