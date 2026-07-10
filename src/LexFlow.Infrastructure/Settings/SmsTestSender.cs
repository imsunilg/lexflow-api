using System.Net.Http.Headers;
using System.Text;
using LexFlow.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace LexFlow.Infrastructure.Settings;

/// <summary>Module 15 §5 SMS Gateway test send — Twilio or MSG91 (India DLT-compliant), per PRD §34.</summary>
public sealed class SmsTestSender(IHttpClientFactory httpClientFactory, IOptions<ExternalGatewayOptions> options) : ISmsTestSender
{
    public async Task<ExternalCallResult> SendTestSmsAsync(SmsGatewayConfig config, string toPhoneNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);

            return config.Provider switch
            {
                "sms_twilio" => await SendViaTwilioAsync(client, config, toPhoneNumber, cancellationToken),
                "sms_msg91" => await SendViaMsg91Async(client, config, toPhoneNumber, cancellationToken),
                _ => new ExternalCallResult(false, $"Unknown SMS provider '{config.Provider}'."),
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new ExternalCallResult(false, $"SMS test failed: {ex.Message}");
        }
    }

    private async Task<ExternalCallResult> SendViaTwilioAsync(HttpClient client, SmsGatewayConfig config, string toPhoneNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.AccountSid) || string.IsNullOrWhiteSpace(config.AuthToken))
        {
            return new ExternalCallResult(false, "Twilio Account SID and Auth Token are required.");
        }

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.AccountSid}:{config.AuthToken}"));
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{options.Value.TwilioBaseUrl}/2010-04-01/Accounts/{config.AccountSid}/Messages.json")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Basic", credentials) },
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["To"] = toPhoneNumber,
                ["From"] = config.SenderId,
                ["Body"] = "LexFlow SMS gateway test (Module 15 §5).",
            }),
        };

        using var response = await client.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode
            ? new ExternalCallResult(true, "Test SMS sent via Twilio.")
            : new ExternalCallResult(false, $"Twilio rejected the test send: {(int)response.StatusCode} {response.ReasonPhrase}");
    }

    private async Task<ExternalCallResult> SendViaMsg91Async(HttpClient client, SmsGatewayConfig config, string toPhoneNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.AuthToken))
        {
            return new ExternalCallResult(false, "MSG91 auth key is required.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.Value.Msg91BaseUrl}/api/v5/flow/")
        {
            Headers = { { "authkey", config.AuthToken } },
            Content = new StringContent(
                $$"""{"sender":"{{config.SenderId}}","mobiles":"{{toPhoneNumber}}","DLT_TE_ID":"{{config.DltEntityId}}"}""",
                Encoding.UTF8,
                "application/json"),
        };

        using var response = await client.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode
            ? new ExternalCallResult(true, "Test SMS sent via MSG91.")
            : new ExternalCallResult(false, $"MSG91 rejected the test send: {(int)response.StatusCode} {response.ReasonPhrase}");
    }
}
