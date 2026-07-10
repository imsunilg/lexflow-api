using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Kb;

public sealed record GetKbJudgmentQuery(Guid Id) : IRequest<KbJudgmentDto?>;

public sealed class GetKbJudgmentQueryHandler(IKbJudgmentService service, ICurrentUserService currentUser) : IRequestHandler<GetKbJudgmentQuery, KbJudgmentDto?>
{
    public Task<KbJudgmentDto?> Handle(GetKbJudgmentQuery request, CancellationToken cancellationToken)
        => service.GetAsync(currentUser.TenantId!.Value, request.Id, cancellationToken);
}

public sealed record GetKbJudgmentsQuery : IRequest<IReadOnlyList<KbJudgmentDto>>;

public sealed class GetKbJudgmentsQueryHandler(IKbJudgmentService service, ICurrentUserService currentUser) : IRequestHandler<GetKbJudgmentsQuery, IReadOnlyList<KbJudgmentDto>>
{
    public Task<IReadOnlyList<KbJudgmentDto>> Handle(GetKbJudgmentsQuery request, CancellationToken cancellationToken)
        => service.GetAllAsync(currentUser.TenantId!.Value, cancellationToken);
}

/// <summary>AC-KB4: "back-link 'pinned in 3 matters'".</summary>
public sealed record GetKbJudgmentPinCountQuery(Guid Id) : IRequest<int>;

public sealed class GetKbJudgmentPinCountQueryHandler(IKbJudgmentService service, ICurrentUserService currentUser) : IRequestHandler<GetKbJudgmentPinCountQuery, int>
{
    public Task<int> Handle(GetKbJudgmentPinCountQuery request, CancellationToken cancellationToken)
        => service.GetPinCountAsync(currentUser.TenantId!.Value, request.Id, cancellationToken);
}
