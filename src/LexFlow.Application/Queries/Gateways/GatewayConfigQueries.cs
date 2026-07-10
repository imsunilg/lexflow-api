using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Gateways;

public sealed record GetGatewayConfigsQuery : IRequest<IReadOnlyList<GatewayConfigDto>>;

public sealed class GetGatewayConfigsQueryHandler(IGatewayConfigService gatewayConfigService, ICurrentUserService currentUser)
    : IRequestHandler<GetGatewayConfigsQuery, IReadOnlyList<GatewayConfigDto>>
{
    public Task<IReadOnlyList<GatewayConfigDto>> Handle(GetGatewayConfigsQuery request, CancellationToken cancellationToken)
        => gatewayConfigService.GetAllAsync(currentUser.TenantId!.Value, cancellationToken);
}
