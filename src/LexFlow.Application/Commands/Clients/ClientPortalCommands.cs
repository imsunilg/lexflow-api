using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Clients;

/// <summary>POST /api/v1/clients/{id}/portal-access {enable}.</summary>
public sealed record SetClientPortalAccessCommand(Guid ClientId, bool Enable) : IRequest<ClientPortalUserDto>;

public sealed class SetClientPortalAccessCommandHandler(IClientService clientService, ICurrentUserService currentUser) : IRequestHandler<SetClientPortalAccessCommand, ClientPortalUserDto>
{
    public Task<ClientPortalUserDto> Handle(SetClientPortalAccessCommand request, CancellationToken cancellationToken)
        => clientService.SetPortalAccessAsync(currentUser.TenantId!.Value, request.ClientId, request.Enable, cancellationToken);
}

/// <summary>POST /api/v1/clients/{id}/portal-access/resend-invite.</summary>
public sealed record ResendClientPortalInviteCommand(Guid ClientId) : IRequest;

public sealed class ResendClientPortalInviteCommandHandler(IClientService clientService, ICurrentUserService currentUser) : IRequestHandler<ResendClientPortalInviteCommand>
{
    public async Task Handle(ResendClientPortalInviteCommand request, CancellationToken cancellationToken)
        => await clientService.ResendPortalInviteAsync(currentUser.TenantId!.Value, request.ClientId, cancellationToken);
}
