using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Comm;

namespace LexFlow.Infrastructure.Fin;

/// <summary>Module 8 User Flow #5: PayPal Orders API (OAuth2 client-credentials token, then a checkout order whose approve link is the "payment link").</summary>
public sealed class PayPalPaymentGateway(IHttpClientFactory httpClientFactory, GatewayCredentialResolver credentials) : IPaymentGateway
{
    public string Provider => "paypal";

    public async Task<PaymentLinkResult> CreatePaymentLinkAsync(Guid tenantId, Guid invoiceId, decimal amount, string currency, string description, CancellationToken cancellationToken = default)
    {
        var resolved = await credentials.ResolveAsync(tenantId, Provider, cancellationToken);
        if (resolved?.Secret is not { } clientSecret)
        {
            return new PaymentLinkResult(false, null, null, "PayPal is not configured for this tenant.");
        }

        using var configDoc = JsonDocument.Parse(resolved.Value.Config.ConfigJson);
        var clientId = configDoc.RootElement.TryGetProperty("clientId", out var clientIdProp) ? clientIdProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return new PaymentLinkResult(false, null, null, "PayPal Client ID is required.");
        }

        try
        {
            using var client = httpClientFactory.CreateClient();
            var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://api-m.paypal.com/v1/oauth2/token")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Basic", basicAuth) },
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" }),
            };

            using var tokenResponse = await client.SendAsync(tokenRequest, cancellationToken);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                return new PaymentLinkResult(false, null, null, $"PayPal rejected the OAuth request: {(int)tokenResponse.StatusCode} {tokenResponse.ReasonPhrase}");
            }

            var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var accessToken = tokenBody.GetProperty("access_token").GetString();

            using var orderRequest = new HttpRequestMessage(HttpMethod.Post, "https://api-m.paypal.com/v2/checkout/orders")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
                Content = JsonContent.Create(new
                {
                    intent = "CAPTURE",
                    purchase_units = new[]
                    {
                        new
                        {
                            reference_id = invoiceId.ToString(),
                            description,
                            amount = new { currency_code = currency, value = amount.ToString("0.00") },
                        },
                    },
                }),
            };

            using var orderResponse = await client.SendAsync(orderRequest, cancellationToken);
            if (!orderResponse.IsSuccessStatusCode)
            {
                return new PaymentLinkResult(false, null, null, $"PayPal rejected the order request: {(int)orderResponse.StatusCode} {orderResponse.ReasonPhrase}");
            }

            var orderBody = await orderResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var orderId = orderBody.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            var approveUrl = orderBody.TryGetProperty("links", out var linksProp)
                ? linksProp.EnumerateArray().FirstOrDefault(l => l.GetProperty("rel").GetString() == "approve").GetProperty("href").GetString()
                : null;

            return new PaymentLinkResult(true, approveUrl, orderId, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or KeyNotFoundException)
        {
            return new PaymentLinkResult(false, null, null, ex.Message);
        }
    }

    public async Task<GatewayPaymentStatusResult> GetPaymentStatusAsync(Guid tenantId, string gatewayRef, CancellationToken cancellationToken = default)
    {
        var resolved = await credentials.ResolveAsync(tenantId, Provider, cancellationToken);
        if (resolved?.Secret is not { } clientSecret)
        {
            return new GatewayPaymentStatusResult(false, null, "PayPal is not configured for this tenant.");
        }

        using var configDoc = JsonDocument.Parse(resolved.Value.Config.ConfigJson);
        var clientId = configDoc.RootElement.TryGetProperty("clientId", out var clientIdProp) ? clientIdProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return new GatewayPaymentStatusResult(false, null, "PayPal Client ID is required.");
        }

        try
        {
            using var client = httpClientFactory.CreateClient();
            var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://api-m.paypal.com/v1/oauth2/token")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Basic", basicAuth) },
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" }),
            };

            using var tokenResponse = await client.SendAsync(tokenRequest, cancellationToken);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                return new GatewayPaymentStatusResult(false, null, $"PayPal rejected the OAuth request: {(int)tokenResponse.StatusCode} {tokenResponse.ReasonPhrase}");
            }

            var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var accessToken = tokenBody.GetProperty("access_token").GetString();

            using var orderRequest = new HttpRequestMessage(HttpMethod.Get, $"https://api-m.paypal.com/v2/checkout/orders/{gatewayRef}")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
            };

            using var orderResponse = await client.SendAsync(orderRequest, cancellationToken);
            if (!orderResponse.IsSuccessStatusCode)
            {
                return new GatewayPaymentStatusResult(false, null, $"PayPal rejected the order status request: {(int)orderResponse.StatusCode} {orderResponse.ReasonPhrase}");
            }

            var orderBody = await orderResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var status = orderBody.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
            return new GatewayPaymentStatusResult(status == "COMPLETED", null, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or KeyNotFoundException)
        {
            return new GatewayPaymentStatusResult(false, null, ex.Message);
        }
    }
}
