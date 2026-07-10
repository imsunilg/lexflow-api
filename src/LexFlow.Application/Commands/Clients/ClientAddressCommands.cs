using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Clients;

/// <summary>POST /api/v1/clients/{id}/addresses.</summary>
public sealed record AddClientAddressCommand(Guid ClientId, string Kind, string Line1, string? Line2, string? City, string? StateCode, string? Postal, string? Country, bool IsPrimaryOfKind) : IRequest<ClientAddressDto>;

public sealed class AddClientAddressCommandHandler(IClientService clientService, ICurrentUserService currentUser) : IRequestHandler<AddClientAddressCommand, ClientAddressDto>
{
    public Task<ClientAddressDto> Handle(AddClientAddressCommand request, CancellationToken cancellationToken)
        => clientService.AddAddressAsync(currentUser.TenantId!.Value, request.ClientId,
            new ClientAddressInput(request.Kind, request.Line1, request.Line2, request.City, request.StateCode, request.Postal, request.Country, request.IsPrimaryOfKind), cancellationToken);
}

/// <summary>PUT /api/v1/clients/{id}/addresses/{addressId}.</summary>
public sealed record UpdateClientAddressCommand(Guid ClientId, Guid AddressId, string Kind, string Line1, string? Line2, string? City, string? StateCode, string? Postal, string? Country, bool IsPrimaryOfKind) : IRequest<ClientAddressDto>;

public sealed class UpdateClientAddressCommandHandler(IClientService clientService, ICurrentUserService currentUser) : IRequestHandler<UpdateClientAddressCommand, ClientAddressDto>
{
    public Task<ClientAddressDto> Handle(UpdateClientAddressCommand request, CancellationToken cancellationToken)
        => clientService.UpdateAddressAsync(currentUser.TenantId!.Value, request.ClientId, request.AddressId,
            new ClientAddressInput(request.Kind, request.Line1, request.Line2, request.City, request.StateCode, request.Postal, request.Country, request.IsPrimaryOfKind), cancellationToken);
}

/// <summary>DELETE /api/v1/clients/{id}/addresses/{addressId}.</summary>
public sealed record DeleteClientAddressCommand(Guid ClientId, Guid AddressId) : IRequest;

public sealed class DeleteClientAddressCommandHandler(IClientService clientService, ICurrentUserService currentUser) : IRequestHandler<DeleteClientAddressCommand>
{
    public async Task Handle(DeleteClientAddressCommand request, CancellationToken cancellationToken)
        => await clientService.DeleteAddressAsync(currentUser.TenantId!.Value, request.ClientId, request.AddressId, cancellationToken);
}
