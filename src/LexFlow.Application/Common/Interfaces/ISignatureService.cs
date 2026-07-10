namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 7 e-signature orchestration — resolves the right ISignatureProvider by name and drives dms.signature_envelopes/signature_signers.</summary>
public interface ISignatureService
{
    Task<SignatureEnvelopeDto> SendForSignatureAsync(Guid tenantId, Guid? actorId, Guid documentId, string provider, IReadOnlyList<SignerInput> signers, CancellationToken cancellationToken = default);

    /// <summary>AC-DOC6: on Completed, downloads the signed PDF + certificate and auto-files it as a new document version.</summary>
    Task HandleWebhookAsync(Guid tenantId, string provider, string rawPayload, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default);

    Task<SignatureEnvelopeDto?> GetByIdAsync(Guid tenantId, Guid envelopeId, CancellationToken cancellationToken = default);
}

public sealed record SignatureEnvelopeDto(Guid Id, Guid DocumentId, string Provider, string? ProviderEnvelopeId, string Status, Guid? CompletedDocVersionId, IReadOnlyList<SignatureSignerDto> Signers);

public sealed record SignatureSignerDto(Guid Id, string Name, string Email, int OrderNo, string Status, DateTimeOffset? SignedAt);
