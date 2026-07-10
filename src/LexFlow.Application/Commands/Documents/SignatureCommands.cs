using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Documents;

/// <summary>POST /api/v1/documents/{id}/signature {provider, signers[]}.</summary>
public sealed record SendForSignatureCommand(Guid DocumentId, string Provider, IReadOnlyList<SignerInput> Signers) : IRequest<SignatureEnvelopeDto>;

public sealed class SendForSignatureCommandHandler(ISignatureService signatureService, ICurrentUserService currentUser) : IRequestHandler<SendForSignatureCommand, SignatureEnvelopeDto>
{
    public Task<SignatureEnvelopeDto> Handle(SendForSignatureCommand request, CancellationToken cancellationToken)
        => signatureService.SendForSignatureAsync(currentUser.TenantId!.Value, currentUser.UserId, request.DocumentId, request.Provider, request.Signers, cancellationToken);
}

/// <summary>POST /api/v1/webhooks/signature/{provider} (public, provider-called). AC-DOC6.</summary>
public sealed record HandleSignatureWebhookCommand(Guid TenantId, string Provider, string RawPayload, IReadOnlyDictionary<string, string> Headers) : IRequest;

public sealed class HandleSignatureWebhookCommandHandler(ISignatureService signatureService) : IRequestHandler<HandleSignatureWebhookCommand>
{
    public async Task Handle(HandleSignatureWebhookCommand request, CancellationToken cancellationToken)
        => await signatureService.HandleWebhookAsync(request.TenantId, request.Provider, request.RawPayload, request.Headers, cancellationToken);
}
