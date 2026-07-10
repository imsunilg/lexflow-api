using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Comm;

namespace LexFlow.Infrastructure.Fin;

/// <summary>Module 8 User Flow #5: Stripe Payment Links API. Credentials resolved the same way as every other gateway in this codebase (core.gateway_configs + Key Vault via GatewayCredentialResolver).</summary>
public sealed class StripePaymentGateway(IHttpClientFactory httpClientFactory, GatewayCredentialResolver credentials) : IPaymentGateway
{
    public string Provider => "stripe";

    public async Task<PaymentLinkResult> CreatePaymentLinkAsync(Guid tenantId, Guid invoiceId, decimal amount, string currency, string description, CancellationToken cancellationToken = default)
    {
        var resolved = await credentials.ResolveAsync(tenantId, Provider, cancellationToken);
        if (resolved?.Secret is not { } secretKey)
        {
            return new PaymentLinkResult(false, null, null, "Stripe is not configured for this tenant.");
        }

        try
        {
            using var client = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v1/payment_links")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", secretKey) },
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["line_items[0][price_data][currency]"] = currency.ToLowerInvariant(),
                    ["line_items[0][price_data][product_data][name]"] = description,
                    ["line_items[0][price_data][unit_amount]"] = ((long)(amount * 100)).ToString(),
                    ["line_items[0][quantity]"] = "1",
                    ["metadata[invoiceId]"] = invoiceId.ToString(),
                }),
            };

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new PaymentLinkResult(false, null, null, $"Stripe rejected the payment link request: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var url = body.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
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
        if (resolved?.Secret is not { } secretKey)
        {
            return new GatewayPaymentStatusResult(false, null, "Stripe is not configured for this tenant.");
        }

        try
        {
            using var client = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.stripe.com/v1/checkout/sessions?payment_link={gatewayRef}")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", secretKey) },
            };

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new GatewayPaymentStatusResult(false, null, $"Stripe rejected the status request: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var session = body.TryGetProperty("data", out var dataProp) ? dataProp.EnumerateArray().FirstOrDefault() : default;
            var paymentStatus = session.ValueKind == JsonValueKind.Object && session.TryGetProperty("payment_status", out var statusProp) ? statusProp.GetString() : null;
            var amountTotal = session.ValueKind == JsonValueKind.Object && session.TryGetProperty("amount_total", out var amountProp) ? amountProp.GetInt64() : 0L;
            return new GatewayPaymentStatusResult(paymentStatus == "paid", amountTotal / 100m, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new GatewayPaymentStatusResult(false, null, ex.Message);
        }
    }
}
