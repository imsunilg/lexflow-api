using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Clients;

/// <summary>GET /api/v1/clients/{id}.</summary>
public sealed record GetClientQuery(Guid ClientId) : IRequest<ClientDto>;

public sealed class GetClientQueryHandler(IClientService clientService, ICurrentUserService currentUser) : IRequestHandler<GetClientQuery, ClientDto>
{
    public async Task<ClientDto> Handle(GetClientQuery request, CancellationToken cancellationToken)
        => await clientService.GetByIdAsync(currentUser.TenantId!.Value, request.ClientId, cancellationToken)
           ?? throw new NotFoundException("Client", request.ClientId);
}

/// <summary>GET /api/v1/clients.</summary>
public sealed record GetClientsQuery(string? Type, string? Status, Guid? OwnerId, Guid? BranchId, string? Query) : IRequest<IReadOnlyList<ClientDto>>;

public sealed class GetClientsQueryHandler(IClientService clientService, ICurrentUserService currentUser) : IRequestHandler<GetClientsQuery, IReadOnlyList<ClientDto>>
{
    public Task<IReadOnlyList<ClientDto>> Handle(GetClientsQuery request, CancellationToken cancellationToken)
        => clientService.GetAllAsync(currentUser.TenantId!.Value, new ClientFilter(request.Type, request.Status, request.OwnerId, request.BranchId, request.Query), cancellationToken);
}

/// <summary>GET /api/v1/clients/{id}/contacts.</summary>
public sealed record GetClientContactsQuery(Guid ClientId) : IRequest<IReadOnlyList<ClientContactDto>>;

public sealed class GetClientContactsQueryHandler(IClientService clientService, ICurrentUserService currentUser) : IRequestHandler<GetClientContactsQuery, IReadOnlyList<ClientContactDto>>
{
    public Task<IReadOnlyList<ClientContactDto>> Handle(GetClientContactsQuery request, CancellationToken cancellationToken)
        => clientService.GetContactsAsync(currentUser.TenantId!.Value, request.ClientId, cancellationToken);
}

/// <summary>GET /api/v1/clients/{id}/addresses.</summary>
public sealed record GetClientAddressesQuery(Guid ClientId) : IRequest<IReadOnlyList<ClientAddressDto>>;

public sealed class GetClientAddressesQueryHandler(IClientService clientService, ICurrentUserService currentUser) : IRequestHandler<GetClientAddressesQuery, IReadOnlyList<ClientAddressDto>>
{
    public Task<IReadOnlyList<ClientAddressDto>> Handle(GetClientAddressesQuery request, CancellationToken cancellationToken)
        => clientService.GetAddressesAsync(currentUser.TenantId!.Value, request.ClientId, cancellationToken);
}

/// <summary>GET /api/v1/clients/{id}/identity-documents.</summary>
public sealed record GetClientIdentityDocumentsQuery(Guid ClientId) : IRequest<IReadOnlyList<ClientIdentityDocumentDto>>;

public sealed class GetClientIdentityDocumentsQueryHandler(IClientService clientService, ICurrentUserService currentUser) : IRequestHandler<GetClientIdentityDocumentsQuery, IReadOnlyList<ClientIdentityDocumentDto>>
{
    public Task<IReadOnlyList<ClientIdentityDocumentDto>> Handle(GetClientIdentityDocumentsQuery request, CancellationToken cancellationToken)
        => clientService.GetIdentityDocumentsAsync(currentUser.TenantId!.Value, request.ClientId, cancellationToken);
}

/// <summary>GET /api/v1/clients/{id}/relationships.</summary>
public sealed record GetClientRelationshipsQuery(Guid ClientId) : IRequest<IReadOnlyList<ClientRelationshipDto>>;

public sealed class GetClientRelationshipsQueryHandler(IClientService clientService, ICurrentUserService currentUser) : IRequestHandler<GetClientRelationshipsQuery, IReadOnlyList<ClientRelationshipDto>>
{
    public Task<IReadOnlyList<ClientRelationshipDto>> Handle(GetClientRelationshipsQuery request, CancellationToken cancellationToken)
        => clientService.GetRelationshipsAsync(currentUser.TenantId!.Value, request.ClientId, cancellationToken);
}

/// <summary>GET /api/v1/clients/{id}/summary. AC-C1: all tab counts in one call.</summary>
public sealed record GetClientSummaryQuery(Guid ClientId) : IRequest<ClientSummaryDto>;

public sealed class GetClientSummaryQueryHandler(IClientService clientService, ICurrentUserService currentUser) : IRequestHandler<GetClientSummaryQuery, ClientSummaryDto>
{
    public Task<ClientSummaryDto> Handle(GetClientSummaryQuery request, CancellationToken cancellationToken)
        => clientService.GetSummaryAsync(currentUser.TenantId!.Value, request.ClientId, cancellationToken);
}

/// <summary>GET /api/v1/clients/{id}/communications?channel=&amp;range=.</summary>
public sealed record GetClientCommunicationsQuery(Guid ClientId, string? Channel, DateTimeOffset? From, DateTimeOffset? To) : IRequest<IReadOnlyList<ClientCommunicationDto>>;

public sealed class GetClientCommunicationsQueryHandler(IClientService clientService, ICurrentUserService currentUser) : IRequestHandler<GetClientCommunicationsQuery, IReadOnlyList<ClientCommunicationDto>>
{
    public Task<IReadOnlyList<ClientCommunicationDto>> Handle(GetClientCommunicationsQuery request, CancellationToken cancellationToken)
        => clientService.GetCommunicationsAsync(currentUser.TenantId!.Value, request.ClientId, request.Channel, request.From, request.To, cancellationToken);
}
