using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.RateCards;

public sealed record GetRateCardsQuery : IRequest<IReadOnlyList<RateCardDto>>;

public sealed class GetRateCardsQueryHandler(IRateCardService service, ICurrentUserService currentUser) : IRequestHandler<GetRateCardsQuery, IReadOnlyList<RateCardDto>>
{
    public Task<IReadOnlyList<RateCardDto>> Handle(GetRateCardsQuery request, CancellationToken cancellationToken)
        => service.GetRateCardsAsync(currentUser.TenantId!.Value, cancellationToken);
}

public sealed record GetBillingArrangementQuery(Guid MatterId) : IRequest<BillingArrangementDto?>;

public sealed class GetBillingArrangementQueryHandler(IRateCardService service, ICurrentUserService currentUser) : IRequestHandler<GetBillingArrangementQuery, BillingArrangementDto?>
{
    public Task<BillingArrangementDto?> Handle(GetBillingArrangementQuery request, CancellationToken cancellationToken)
        => service.GetBillingArrangementAsync(currentUser.TenantId!.Value, request.MatterId, cancellationToken);
}
