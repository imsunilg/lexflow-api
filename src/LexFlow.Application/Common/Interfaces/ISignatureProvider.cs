namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// DocuSign/Adobe Sign abstraction (Module 7: "send for e-signature (DocuSign / Adobe
/// Sign): pick doc + signers + field placement → provider envelope → status webhook →
/// signed copy auto-filed as new version"). One implementation per provider, resolved
/// by <see cref="ProviderName"/>; behind an interface so it's mockable in tests — no
/// test in this codebase should make a real call to either provider's API.
/// </summary>
public interface ISignatureProvider
{
    /// <summary>Matches dms.signature_envelopes.provider — "DocuSign" or "AdobeSign".</summary>
    string ProviderName { get; }

    Task<SendEnvelopeResult> SendEnvelopeAsync(byte[] documentContent, string fileName, IReadOnlyList<SignerInput> signers, CancellationToken cancellationToken = default);

    /// <summary>Parses a provider webhook payload into a normalized event; each provider has its own payload shape and signature-verification scheme.</summary>
    SignatureWebhookEvent ParseWebhook(string rawPayload, IReadOnlyDictionary<string, string> headers);

    /// <summary>Downloads the completed, signed document (+ completion certificate merged or appended, provider-dependent) once the envelope is Completed.</summary>
    Task<byte[]> DownloadCompletedDocumentAsync(string providerEnvelopeId, CancellationToken cancellationToken = default);
}

public sealed record SignerInput(string Name, string Email, int OrderNo);

public sealed record SendEnvelopeResult(string ProviderEnvelopeId, string Status);

public sealed record SignatureWebhookEvent(string ProviderEnvelopeId, string Status, string? SignerEmail);
