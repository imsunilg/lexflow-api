using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.NumberSeries;

public sealed record GetNumberSeriesListQuery : IRequest<IReadOnlyList<NumberSeriesDto>>;

public sealed class GetNumberSeriesListQueryHandler(INumberSeriesService numberSeriesService, ICurrentUserService currentUser)
    : IRequestHandler<GetNumberSeriesListQuery, IReadOnlyList<NumberSeriesDto>>
{
    public Task<IReadOnlyList<NumberSeriesDto>> Handle(GetNumberSeriesListQuery request, CancellationToken cancellationToken)
        => numberSeriesService.GetAllAsync(currentUser.TenantId!.Value, cancellationToken);
}

/// <summary>AC-S2: preview must match the actual next generated number.</summary>
public sealed record PreviewNextNumberQuery(Guid Id) : IRequest<string>;

public sealed class PreviewNextNumberQueryHandler(INumberSeriesService numberSeriesService, ICurrentUserService currentUser)
    : IRequestHandler<PreviewNextNumberQuery, string>
{
    public Task<string> Handle(PreviewNextNumberQuery request, CancellationToken cancellationToken)
        => numberSeriesService.PreviewNextAsync(currentUser.TenantId!.Value, request.Id, cancellationToken);
}
