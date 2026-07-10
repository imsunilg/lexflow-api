using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Clients;

/// <summary>POST /api/v1/clients/{id}/contacts.</summary>
public sealed record AddClientContactCommand(Guid ClientId, string Name, string? Designation, string? Email, string? Phone, bool IsPrimary) : IRequest<ClientContactDto>;

public sealed class AddClientContactCommandHandler(IClientService clientService, ICurrentUserService currentUser) : IRequestHandler<AddClientContactCommand, ClientContactDto>
{
    public Task<ClientContactDto> Handle(AddClientContactCommand request, CancellationToken cancellationToken)
        => clientService.AddContactAsync(currentUser.TenantId!.Value, request.ClientId, new ClientContactInput(request.Name, request.Designation, request.Email, request.Phone, request.IsPrimary), cancellationToken);
}

/// <summary>PUT /api/v1/clients/{id}/contacts/{contactId}.</summary>
public sealed record UpdateClientContactCommand(Guid ClientId, Guid ContactId, string Name, string? Designation, string? Email, string? Phone, bool IsPrimary) : IRequest<ClientContactDto>;

public sealed class UpdateClientContactCommandHandler(IClientService clientService, ICurrentUserService currentUser) : IRequestHandler<UpdateClientContactCommand, ClientContactDto>
{
    public Task<ClientContactDto> Handle(UpdateClientContactCommand request, CancellationToken cancellationToken)
        => clientService.UpdateContactAsync(currentUser.TenantId!.Value, request.ClientId, request.ContactId, new ClientContactInput(request.Name, request.Designation, request.Email, request.Phone, request.IsPrimary), cancellationToken);
}

/// <summary>DELETE /api/v1/clients/{id}/contacts/{contactId}.</summary>
public sealed record DeleteClientContactCommand(Guid ClientId, Guid ContactId) : IRequest;

public sealed class DeleteClientContactCommandHandler(IClientService clientService, ICurrentUserService currentUser) : IRequestHandler<DeleteClientContactCommand>
{
    public async Task Handle(DeleteClientContactCommand request, CancellationToken cancellationToken)
        => await clientService.DeleteContactAsync(currentUser.TenantId!.Value, request.ClientId, request.ContactId, cancellationToken);
}
