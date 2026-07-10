using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Branches;

/// <summary>CRUD /api/v1/branches (PRD §17). Deletion blocked while users/matters attached (PRD Module 14 Validation).</summary>
public sealed record CreateBranchCommand(string Name, string Code, string AddressJson, string Tz, string? Gstin, string? SeriesPrefix) : IRequest<BranchDto>;

public sealed class CreateBranchCommandHandler(IBranchService branchService, ICurrentUserService currentUser) : IRequestHandler<CreateBranchCommand, BranchDto>
{
    public Task<BranchDto> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
        => branchService.CreateAsync(
            currentUser.TenantId!.Value,
            new CreateBranchInput(request.Name, request.Code, request.AddressJson, request.Tz, request.Gstin, request.SeriesPrefix),
            cancellationToken);
}

public sealed record UpdateBranchCommand(Guid BranchId, string Name, string Code, string AddressJson, string Tz, string? Gstin, string? SeriesPrefix) : IRequest<BranchDto>;

public sealed class UpdateBranchCommandHandler(IBranchService branchService, ICurrentUserService currentUser) : IRequestHandler<UpdateBranchCommand, BranchDto>
{
    public Task<BranchDto> Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
        => branchService.UpdateAsync(
            currentUser.TenantId!.Value,
            request.BranchId,
            new CreateBranchInput(request.Name, request.Code, request.AddressJson, request.Tz, request.Gstin, request.SeriesPrefix),
            cancellationToken);
}

public sealed record DeleteBranchCommand(Guid BranchId) : IRequest;

public sealed class DeleteBranchCommandHandler(IBranchService branchService, ICurrentUserService currentUser) : IRequestHandler<DeleteBranchCommand>
{
    public async Task Handle(DeleteBranchCommand request, CancellationToken cancellationToken)
        => await branchService.DeleteAsync(currentUser.TenantId!.Value, request.BranchId, cancellationToken);
}
