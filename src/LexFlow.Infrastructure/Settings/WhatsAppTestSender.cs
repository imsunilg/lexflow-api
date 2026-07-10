using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LexFlow.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace LexFlow.Infrastructure.Settings;

/// <summary>Module 15 §6 WhatsApp Cloud API test send + template catalog sync (PRD §34: "Graph API + webhooks").</summary>
public sealed class WhatsAppTestSender(IHttpClientFactory httpClientFactory, IOptions<ExternalGatewayOptions> options) : IWhatsAppTestSender
{
    public async Task<ExternalCallResult> SendTestMessageAsync(WhatsAppConfig config, string toPhoneNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = CreateClient(config);
            var payload = JsonSerializer.Serialize(new
            {
                messaging_product = "whatsapp",
                to = toPhoneNumber,
                type = "text",
                text = new { body = "LexFlow WhatsApp gateway test (Module 15 §6)." },
            });

            using var response = await client.PostAsync(
                $"{options.Value.WhatsAppGraphBaseUrl}/{config.PhoneNumberId}/messages",
                new StringContent(payload, Encoding.UTF8, "application/json"),
                cancellationToken);

            return response.IsSuccessStatusCode
                ? new ExternalCallResult(true, "Test WhatsApp message sent.")
                : new ExternalCallResult(false, $"WhatsApp Cloud API rejected the test send: {(int)response.StatusCode} {response.ReasonPhrase}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new ExternalCallResult(false, $"WhatsApp test failed: {ex.Message}");
        }
    }

    public async Task<ExternalCallResult> SyncTemplatesAsync(WhatsAppConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = CreateClient(config);
            using var response = await client.GetAsync(
                $"{options.Value.WhatsAppGraphBaseUrl}/{config.WabaId}/message_templates",
                cancellationToken);

            return response.IsSuccessStatusCode
                ? new ExternalCallResult(true, "Template catalog synced.")
                : new ExternalCallResult(false, $"WhatsApp Cloud API rejected the sync: {(int)response.StatusCode} {response.ReasonPhrase}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new ExternalCallResult(false, $"WhatsApp template sync failed: {ex.Message}");
        }
    }

    private HttpClient CreateClient(WhatsAppConfig config)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.AccessToken);
        return client;
    }
}
