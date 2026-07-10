using System.Net.Http.Headers;
using System.Text;
using LexFlow.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace LexFlow.Infrastructure.Settings;

/// <summary>
/// Module 15 §7 Payment Gateways live credential verification. AC-S1: "invalid
/// Razorpay key cannot be saved (verify fails)" — the same pattern applies to
/// Stripe/PayPal: a lightweight authenticated read-only call against each provider's
/// real API, any non-2xx or network failure fails verification.
/// </summary>
public sealed class PaymentGatewayVerifier(IHttpClientFactory httpClientFactory, IOptions<ExternalGatewayOptions> options) : IPaymentGatewayVerifier
{
    public async Task<ExternalCallResult> VerifyAsync(string provider, string configJson, string? secret, bool isTestMode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return new ExternalCallResult(false, "A secret key is required to verify this gateway.");
        }

        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);

            return provider switch
            {
                "stripe" => await VerifyStripeAsync(client, secret, cancellationToken),
                "razorpay" => await VerifyRazorpayAsync(client, configJson, secret, cancellationToken),
                "paypal" => await VerifyPayPalAsync(client, configJson, secret, cancellationToken),
                _ => new ExternalCallResult(false, $"Unknown payment gateway '{provider}'."),
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new ExternalCallResult(false, $"Gateway verification failed: {ex.Message}");
        }
    }

    private async Task<ExternalCallResult> VerifyStripeAsync(HttpClient client, string secretKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{options.Value.StripeBaseUrl}/v1/account");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);

        using var response = await client.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode
            ? new ExternalCallResult(true, "Stripe credentials verified.")
            : new ExternalCallResult(false, $"Stripe rejected the credentials: {(int)response.StatusCode} {response.ReasonPhrase}");
    }

    private async Task<ExternalCallResult> VerifyRazorpayAsync(HttpClient client, string configJson, string secretKey, CancellationToken cancellationToken)
    {
        var keyId = ExtractJsonField(configJson, "keyId");
        if (string.IsNullOrWhiteSpace(keyId))
        {
            return new ExternalCallResult(false, "Razorpay Key ID is required.");
        }

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{secretKey}"));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{options.Value.RazorpayBaseUrl}/v1/payments?count=1");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var response = await client.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode
            ? new ExternalCallResult(true, "Razorpay credentials verified.")
            : new ExternalCallResult(false, $"Razorpay rejected the credentials: {(int)response.StatusCode} {response.ReasonPhrase}");
    }

    private async Task<ExternalCallResult> VerifyPayPalAsync(HttpClient client, string configJson, string secretKey, CancellationToken cancellationToken)
    {
        var clientId = ExtractJsonField(configJson, "clientId");
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return new ExternalCallResult(false, "PayPal Client ID is required.");
        }

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secretKey}"));
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.Value.PayPalBaseUrl}/v1/oauth2/token")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Basic", credentials) },
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" }),
        };

        using var response = await client.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode
            ? new ExternalCallResult(true, "PayPal credentials verified.")
            : new ExternalCallResult(false, $"PayPal rejected the credentials: {(int)response.StatusCode} {response.ReasonPhrase}");
    }

    private static string? ExtractJsonField(string json, string propertyName)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty(propertyName, out var value) ? value.GetString() : null;
    }
}
