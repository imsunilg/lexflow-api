using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Comm;

namespace LexFlow.Infrastructure.Fin;

/// <summary>Module 8 User Flow #5: Razorpay Payment Links API (also carries UPI intent automatically for Indian cards/UPI — no extra flag needed on this endpoint).</summary>
public sealed class RazorpayPaymentGateway(IHttpClientFactory httpClientFactory, GatewayCredentialResolver credentials) : IPaymentGateway
{
    public string Provider => "razorpay";

    public async Task<PaymentLinkResult> CreatePaymentLinkAsync(Guid tenantId, Guid invoiceId, decimal amount, string currency, string description, CancellationToken cancellationToken = default)
    {
        var resolved = await credentials.ResolveAsync(tenantId, Provider, cancellationToken);
        if (resolved?.Secret is not { } keySecret)
        {
            return new PaymentLinkResult(false, null, null, "Razorpay is not configured for this tenant.");
        }

        using var configDoc = JsonDocument.Parse(resolved.Value.Config.ConfigJson);
        var keyId = configDoc.RootElement.TryGetProperty("keyId", out var keyIdProp) ? keyIdProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(keyId))
        {
            return new PaymentLinkResult(false, null, null, "Razorpay Key ID is required.");
        }

        try
        {
            using var client = httpClientFactory.CreateClient();
            var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.razorpay.com/v1/payment_links")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Basic", basicAuth) },
                Content = JsonContent.Create(new
                {
                    amount = (long)(amount * 100),
                    currency,
                    description,
                    notes = new { invoiceId = invoiceId.ToString() },
                }),
            };

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new PaymentLinkResult(false, null, null, $"Razorpay rejected the payment link request: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var url = body.TryGetProperty("short_url", out var urlProp) ? urlProp.GetString() : null;
            var id = body.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            return new PaymentLinkResult(true, url, id, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new PaymentLinkResult(false, null, null, ex.Message);
        }
    }

    public async Task<GatewayPaymentStatusResult> GetPaymentStatusAsync(Guid tenantId, string gatewayRef, CancellationToken cancellationToken = default)
    {
        var resolved = await credentials.ResolveAsync(tenantId, Provider, cancellationToken);
        if (resolved?.Secret is not { } keySecret)
        {
            return new GatewayPaymentStatusResult(false, null, "Razorpay is not configured for this tenant.");
        }

        using var configDoc = JsonDocument.Parse(resolved.Value.Config.ConfigJson);
        var keyId = configDoc.RootElement.TryGetProperty("keyId", out var keyIdProp) ? keyIdProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(keyId))
        {
            return new GatewayPaymentStatusResult(false, null, "Razorpay Key ID is required.");
        }

        try
        {
            using var client = httpClientFactory.CreateClient();
            var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.razorpay.com/v1/payment_links/{gatewayRef}")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Basic", basicAuth) },
            };

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new GatewayPaymentStatusResult(false, null, $"Razorpay rejected the status request: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var status = body.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
            var amountPaid = body.TryGetProperty("amount_paid", out var amountProp) ? amountProp.GetInt64() : 0L;
            return new GatewayPaymentStatusResult(status == "paid", amountPaid / 100m, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new GatewayPaymentStatusResult(false, null, ex.Message);
        }
    }
}
