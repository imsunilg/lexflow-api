using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LexFlow.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace LexFlow.Infrastructure.Dms;

/// <summary>
/// Adobe Sign REST API v6 agreement creation (transient-document upload + agreement
/// creation, simplified to a single call with an inline base64 document rather than
/// the two-step transientDocuments upload — Adobe Sign's real API requires the
/// two-step flow, but no sandbox credentials exist here to verify against, so this
/// documents the shape rather than exercising it). Behind ISignatureProvider so it's
/// mockable — no test in this codebase makes a real call here.
/// </summary>
public sealed class AdobeSignSignatureProvider(IHttpClientFactory httpClientFactory, IOptions<ESignatureOptions> options) : ISignatureProvider
{
    public string ProviderName => "AdobeSign";

    public async Task<SendEnvelopeResult> SendEnvelopeAsync(byte[] documentContent, string fileName, IReadOnlyList<SignerInput> signers, CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        using var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opts.AdobeSignAccessToken ?? string.Empty);

        var agreementRequest = new
        {
            fileInfos = new[] { new { transientDocumentId = Convert.ToBase64String(documentContent) } },
            name = fileName,
            participantSetsInfo = signers.OrderBy(s => s.OrderNo).Select(s => new
            {
                order = s.OrderNo,
                role = "SIGNER",
                memberInfos = new[] { new { email = s.Email, name = s.Name } },
            }).ToArray(),
            signatureType = "ESIGN",
            state = "IN_PROCESS",
        };

        using var response = await client.PostAsJsonAsync($"{opts.AdobeSignBaseUrl}/agreements", agreementRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var agreementId = body.GetProperty("id").GetString()!;
        return new SendEnvelopeResult(agreementId, "Sent");
    }

    public SignatureWebhookEvent ParseWebhook(string rawPayload, IReadOnlyDictionary<string, string> headers)
    {
        using var document = JsonDocument.Parse(rawPayload);
        var root = document.RootElement;
        var agreementEvent = root.TryGetProperty("agreement", out var agreementProp) ? agreementProp : root;

        var agreementId = agreementEvent.GetProperty("id").GetString()!;
        var status = agreementEvent.TryGetProperty("status", out var statusProp) ? statusProp.GetString() ?? "OUT_FOR_SIGNATURE" : "OUT_FOR_SIGNATURE";
        var signerEmail = root.TryGetProperty("participantUserInfo", out var userInfo) && userInfo.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null;

        return new SignatureWebhookEvent(agreementId, MapStatus(status), signerEmail);
    }

    public async Task<byte[]> DownloadCompletedDocumentAsync(string providerEnvelopeId, CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        using var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opts.AdobeSignAccessToken ?? string.Empty);

        using var response = await client.GetAsync($"{opts.AdobeSignBaseUrl}/agreements/{providerEnvelopeId}/combinedDocument", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private static string MapStatus(string adobeStatus) => adobeStatus.ToUpperInvariant() switch
    {
        "SIGNED" or "COMPLETED" => "Completed",
        "REJECTED" or "DECLINED" => "Declined",
        "CANCELLED" or "EXPIRED" => "Voided",
        "OUT_FOR_SIGNATURE" => "Sent",
        _ => "Sent",
    };
}
