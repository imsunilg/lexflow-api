using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LexFlow.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace LexFlow.Infrastructure.Dms;

/// <summary>
/// DocuSign eSignature REST API v2.1 envelope creation (simplified: an integration-key
/// bearer token is used directly rather than the full OAuth JWT-grant flow, since no
/// real DocuSign sandbox credentials are provisioned in this codebase). Behind
/// ISignatureProvider so it's mockable — no test in this codebase makes a real call here.
/// </summary>
public sealed class DocuSignSignatureProvider(IHttpClientFactory httpClientFactory, IOptions<ESignatureOptions> options) : ISignatureProvider
{
    public string ProviderName => "DocuSign";

    public async Task<SendEnvelopeResult> SendEnvelopeAsync(byte[] documentContent, string fileName, IReadOnlyList<SignerInput> signers, CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        using var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opts.DocuSignAccessToken ?? string.Empty);

        var envelopeRequest = new
        {
            emailSubject = "Please sign this document",
            documents = new[]
            {
                new
                {
                    documentBase64 = Convert.ToBase64String(documentContent),
                    name = fileName,
                    fileExtension = Path.GetExtension(fileName).TrimStart('.'),
                    documentId = "1",
                },
            },
            recipients = new
            {
                signers = signers.Select(s => new
                {
                    email = s.Email,
                    name = s.Name,
                    recipientId = s.OrderNo.ToString(),
                    routingOrder = s.OrderNo.ToString(),
                }).ToArray(),
            },
            status = "sent",
        };

        using var response = await client.PostAsJsonAsync($"{opts.DocuSignBaseUrl}/v2.1/accounts/{opts.DocuSignAccountId}/envelopes", envelopeRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var envelopeId = body.GetProperty("envelopeId").GetString()!;
        var status = body.TryGetProperty("status", out var statusProp) ? statusProp.GetString() ?? "Sent" : "Sent";
        return new SendEnvelopeResult(envelopeId, status);
    }

    public SignatureWebhookEvent ParseWebhook(string rawPayload, IReadOnlyDictionary<string, string> headers)
    {
        // DocuSign Connect posts an envelope-status JSON payload (or legacy XML — JSON is
        // used here since it's the modern Connect configuration and requires no XML parsing).
        using var document = JsonDocument.Parse(rawPayload);
        var root = document.RootElement;

        var envelopeId = root.TryGetProperty("envelopeId", out var envelopeIdProp) ? envelopeIdProp.GetString()! : root.GetProperty("data").GetProperty("envelopeId").GetString()!;
        var status = root.TryGetProperty("status", out var statusProp) ? statusProp.GetString() ?? "Sent" : "Sent";
        var signerEmail = root.TryGetProperty("recipientEmail", out var emailProp) ? emailProp.GetString() : null;

        return new SignatureWebhookEvent(envelopeId, MapStatus(status), signerEmail);
    }

    public async Task<byte[]> DownloadCompletedDocumentAsync(string providerEnvelopeId, CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        using var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opts.DocuSignAccessToken ?? string.Empty);

        using var response = await client.GetAsync($"{opts.DocuSignBaseUrl}/v2.1/accounts/{opts.DocuSignAccountId}/envelopes/{providerEnvelopeId}/documents/combined", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private static string MapStatus(string docuSignStatus) => docuSignStatus.ToLowerInvariant() switch
    {
        "completed" => "Completed",
        "declined" => "Declined",
        "voided" => "Voided",
        "delivered" => "Viewed",
        _ => "Sent",
    };
}
