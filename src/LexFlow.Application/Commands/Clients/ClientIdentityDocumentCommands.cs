using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Clients;

/// <summary>POST /api/v1/clients/{id}/identity-documents (multipart). DocNumber is encrypted before persistence (Module 3 KYC).</summary>
public sealed record AddClientIdentityDocumentCommand(Guid ClientId, string DocKind, string DocNumber, DateOnly? ExpiryDate, Guid? DocumentId) : IRequest<ClientIdentityDocumentDto>;

public sealed class AddClientIdentityDocumentCommandHandler(IClientService clientService, ICurrentUserService currentUser) : IRequestHandler<AddClientIdentityDocumentCommand, ClientIdentityDocumentDto>
{
    public Task<ClientIdentityDocumentDto> Handle(AddClientIdentityDocumentCommand request, CancellationToken cancellationToken)
        => clientService.AddIdentityDocumentAsync(currentUser.TenantId!.Value, request.ClientId, request.DocKind, request.DocNumber, request.ExpiryDate, request.DocumentId, cancellationToken);
}

/// <summary>PATCH /api/v1/clients/{id}/identity-documents/{docId}/verify.</summary>
public sealed record VerifyClientIdentityDocumentCommand(Guid ClientId, Guid DocumentId, bool Approve) : IRequest<ClientIdentityDocumentDto>;

public sealed class VerifyClientIdentityDocumentCommandHandler(IClientService clientService, ICurrentUserService currentUser) : IRequestHandler<VerifyClientIdentityDocumentCommand, ClientIdentityDocumentDto>
{
    public Task<ClientIdentityDocumentDto> Handle(VerifyClientIdentityDocumentCommand request, CancellationToken cancellationToken)
        => clientService.VerifyIdentityDocumentAsync(currentUser.TenantId!.Value, request.ClientId, request.DocumentId, currentUser.UserId!.Value, request.Approve, cancellationToken);
}
