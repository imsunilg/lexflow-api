using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Clients;

/// <summary>POST /api/v1/clients/{id}/relationships {relatedClientId?, personName?, relationType}.</summary>
public sealed record AddClientRelationshipCommand(Guid ClientId, Guid? RelatedClientId, string? PersonName, string RelationType) : IRequest<ClientRelationshipDto>;

public sealed class AddClientRelationshipCommandHandler(IClientService clientService, ICurrentUserService currentUser) : IRequestHandler<AddClientRelationshipCommand, ClientRelationshipDto>
{
    public Task<ClientRelationshipDto> Handle(AddClientRelationshipCommand request, CancellationToken cancellationToken)
        => clientService.AddRelationshipAsync(currentUser.TenantId!.Value, request.ClientId, request.RelatedClientId, request.PersonName, request.RelationType, cancellationToken);
}
