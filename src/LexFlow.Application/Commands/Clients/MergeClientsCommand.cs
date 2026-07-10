using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Clients;

/// <summary>POST /api/v1/clients/merge {survivorId, duplicateId, fieldChoices}. AC-C3.</summary>
public sealed record MergeClientsCommand(Guid SurvivorId, Guid DuplicateId, IReadOnlyDictionary<string, string> FieldChoices) : IRequest;

public sealed class MergeClientsCommandHandler(IClientService clientService, ICurrentUserService currentUser) : IRequestHandler<MergeClientsCommand>
{
    public async Task Handle(MergeClientsCommand request, CancellationToken cancellationToken)
        => await clientService.MergeAsync(currentUser.TenantId!.Value, request.SurvivorId, request.DuplicateId, request.FieldChoices, cancellationToken);
}
