using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Clients;

/// <summary>POST /api/v1/clients (PRD §17, Module 3).</summary>
public sealed record CreateClientCommand(
    string Type,
    string? FirstName,
    string? LastName,
    string? LegalName,
    string? Email,
    string? PhoneE164,
    string? Gstin,
    string? Cin,
    Guid? OwnerId,
    Guid? BranchId,
    Guid? SourceLeadId,
    IReadOnlyList<ClientContactInput>? Contacts) : IRequest<ClientDto>;

public sealed class CreateClientCommandHandler(IClientService clientService, ICurrentUserService currentUser) : IRequestHandler<CreateClientCommand, ClientDto>
{
    public Task<ClientDto> Handle(CreateClientCommand request, CancellationToken cancellationToken)
        => clientService.CreateAsync(
            currentUser.TenantId!.Value,
            currentUser.UserId,
            new CreateClientInput(
                request.Type, request.FirstName, request.LastName, request.LegalName, request.Email, request.PhoneE164,
                request.Gstin, request.Cin, request.OwnerId, request.BranchId, request.SourceLeadId, request.Contacts),
            cancellationToken);
}

/// <summary>PUT /api/v1/clients/{id}.</summary>
public sealed record UpdateClientCommand(
    Guid ClientId,
    string? FirstName,
    string? LastName,
    string? LegalName,
    string? Email,
    string? PhoneE164,
    string? Gstin,
    string? Cin,
    decimal? CreditLimit,
    Guid? OwnerId,
    Guid? BranchId) : IRequest<ClientDto>;

public sealed class UpdateClientCommandHandler(IClientService clientService, ICurrentUserService currentUser) : IRequestHandler<UpdateClientCommand, ClientDto>
{
    public Task<ClientDto> Handle(UpdateClientCommand request, CancellationToken cancellationToken)
        => clientService.UpdateAsync(
            currentUser.TenantId!.Value,
            request.ClientId,
            new UpdateClientInput(request.FirstName, request.LastName, request.LegalName, request.Email, request.PhoneE164, request.Gstin, request.Cin, request.CreditLimit, request.OwnerId, request.BranchId),
            cancellationToken);
}

/// <summary>DELETE /api/v1/clients/{id} — soft; blocked if open matters/unpaid invoices (AC-C4).</summary>
public sealed record DeleteClientCommand(Guid ClientId) : IRequest;

public sealed class DeleteClientCommandHandler(IClientService clientService, ICurrentUserService currentUser) : IRequestHandler<DeleteClientCommand>
{
    public async Task Handle(DeleteClientCommand request, CancellationToken cancellationToken)
        => await clientService.DeleteAsync(currentUser.TenantId!.Value, request.ClientId, cancellationToken);
}
