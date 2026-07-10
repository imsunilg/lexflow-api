using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Dms;

/// <summary>
/// Module 7 e-signature orchestration. AC-DOC6: "DocuSign round-trip files signed PDF +
/// completion certificate automatically" — <see cref="HandleWebhookAsync"/> is where
/// that happens, the moment a webhook reports the envelope Completed.
/// </summary>
public sealed class SignatureService(LexFlowDbContext db, IEnumerable<ISignatureProvider> providers, IBlobStorageService blobStorage) : ISignatureService
{
    private const string Container = "documents";

    public async Task<SignatureEnvelopeDto> SendForSignatureAsync(Guid tenantId, Guid? actorId, Guid documentId, string provider, IReadOnlyList<SignerInput> signers, CancellationToken cancellationToken = default)
    {
        var document = await db.Documents.SingleOrDefaultAsync(d => d.TenantId == tenantId && d.Id == documentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Document), documentId);

        if (document.CurrentVersionId is null)
        {
            throw new ConflictException("Document has no current version to send for signature.", "NO_CURRENT_VERSION");
        }

        var signatureProvider = ResolveProvider(provider);
        var version = await db.DocumentVersions.SingleAsync(v => v.Id == document.CurrentVersionId, cancellationToken);
        var content = await blobStorage.DownloadAsync(Container, version.BlobPath, cancellationToken);

        var envelope = new SignatureEnvelope(tenantId, documentId, provider);
        await db.SignatureEnvelopes.AddAsync(envelope, cancellationToken);

        var signerEntities = signers.Select(s => new SignatureSigner(tenantId, envelope.Id, s.Name, s.Email, s.OrderNo)).ToList();
        await db.SignatureSigners.AddRangeAsync(signerEntities, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var result = await signatureProvider.SendEnvelopeAsync(content, Path.GetFileName(version.BlobPath), signers, cancellationToken);
        envelope.SetProviderEnvelopeId(result.ProviderEnvelopeId);
        await db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(envelope, cancellationToken);
    }

    public async Task HandleWebhookAsync(Guid tenantId, string provider, string rawPayload, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        var signatureProvider = ResolveProvider(provider);
        var webhookEvent = signatureProvider.ParseWebhook(rawPayload, headers);

        var envelope = await db.SignatureEnvelopes.SingleOrDefaultAsync(e => e.TenantId == tenantId && e.Provider == provider && e.ProviderEnvelopeId == webhookEvent.ProviderEnvelopeId, cancellationToken);
        if (envelope is null)
        {
            return;
        }

        if (webhookEvent.SignerEmail is not null)
        {
            var signer = await db.SignatureSigners.SingleOrDefaultAsync(s => s.EnvelopeId == envelope.Id && s.Email == webhookEvent.SignerEmail, cancellationToken);
            signer?.SetStatus(webhookEvent.Status == "Completed" ? "Signed" : webhookEvent.Status);
        }

        if (webhookEvent.Status == "Completed" && envelope.Status != "Completed")
        {
            // AC-DOC6: download the signed PDF + certificate and auto-file it as a new
            // document version — never overwriting history, same as any other new version.
            var signedContent = await signatureProvider.DownloadCompletedDocumentAsync(envelope.ProviderEnvelopeId!, cancellationToken);
            var document = await db.Documents.SingleAsync(d => d.Id == envelope.DocumentId, cancellationToken);

            var nextVersionNo = await db.DocumentVersions.Where(v => v.TenantId == tenantId && v.DocumentId == document.Id).MaxAsync(v => v.VersionNo, cancellationToken) + 1;
            var blobPath = $"{tenantId:N}/{document.Id:N}/v{nextVersionNo}-signed.pdf";
            await blobStorage.UploadAsync(Container, blobPath, signedContent, "application/pdf", cancellationToken);

            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(signedContent)).ToLowerInvariant();
            var signedVersion = new DocumentVersion(tenantId, document.Id, nextVersionNo, blobPath, signedContent.LongLength, "application/pdf", hash, uploadedBy: null);
            await db.DocumentVersions.AddAsync(signedVersion, cancellationToken);

            var outbox = new DocumentIndexOutbox(tenantId, document.Id, signedVersion.Id, "Index");
            await db.DocumentIndexOutbox.AddAsync(outbox, cancellationToken);

            envelope.Complete(signedVersion.Id);
        }
        else
        {
            envelope.SetStatus(webhookEvent.Status);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SignatureEnvelopeDto?> GetByIdAsync(Guid tenantId, Guid envelopeId, CancellationToken cancellationToken = default)
    {
        var envelope = await db.SignatureEnvelopes.SingleOrDefaultAsync(e => e.TenantId == tenantId && e.Id == envelopeId, cancellationToken);
        return envelope is null ? null : await ToDtoAsync(envelope, cancellationToken);
    }

    private ISignatureProvider ResolveProvider(string provider)
        => providers.FirstOrDefault(p => p.ProviderName == provider)
           ?? throw new ConflictException($"Unknown signature provider '{provider}'.", "UNKNOWN_SIGNATURE_PROVIDER");

    private async Task<SignatureEnvelopeDto> ToDtoAsync(SignatureEnvelope envelope, CancellationToken cancellationToken)
    {
        var signers = await db.SignatureSigners.Where(s => s.EnvelopeId == envelope.Id).OrderBy(s => s.OrderNo).ToListAsync(cancellationToken);
        return new SignatureEnvelopeDto(
            envelope.Id, envelope.DocumentId, envelope.Provider, envelope.ProviderEnvelopeId, envelope.Status, envelope.CompletedDocVersionId,
            signers.Select(s => new SignatureSignerDto(s.Id, s.Name, s.Email, s.OrderNo, s.Status, s.SignedAt)).ToList());
    }
}
