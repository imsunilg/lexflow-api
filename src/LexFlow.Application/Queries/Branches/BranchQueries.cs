using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Branches;

public sealed record GetBranchesQuery : IRequest<IReadOnlyList<BranchDto>>;

public sealed class GetBranchesQueryHandler(IBranchService branchService, ICurrentUserService currentUser) : IRequestHandler<GetBranchesQuery, IReadOnlyList<BranchDto>>
{
    public Task<IReadOnlyList<BranchDto>> Handle(GetBranchesQuery request, CancellationToken cancellationToken)
        => branchService.GetAllAsync(currentUser.TenantId!.Value, cancellationToken);
}

public sealed record GetBranchQuery(Guid BranchId) : IRequest<BranchDto>;

public sealed class GetBranchQueryHandler(IBranchService branchService, ICurrentUserService currentUser) : IRequestHandler<GetBranchQuery, BranchDto>
{
    public async Task<BranchDto> Handle(GetBranchQuery request, CancellationToken cancellationToken)
        => await branchService.GetByIdAsync(currentUser.TenantId!.Value, request.BranchId, cancellationToken)
           ?? throw new NotFoundException("Branch", request.BranchId);
}
