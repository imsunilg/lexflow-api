using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.TaxRates;

public sealed record GetTaxRatesQuery : IRequest<IReadOnlyList<TaxRateDto>>;

public sealed class GetTaxRatesQueryHandler(ITaxRateService taxRateService, ICurrentUserService currentUser) : IRequestHandler<GetTaxRatesQuery, IReadOnlyList<TaxRateDto>>
{
    public Task<IReadOnlyList<TaxRateDto>> Handle(GetTaxRatesQuery request, CancellationToken cancellationToken)
        => taxRateService.GetAllAsync(currentUser.TenantId!.Value, cancellationToken);
}
