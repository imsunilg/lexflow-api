using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Hearings;

public sealed record GetHearingQuery(Guid HearingId) : IRequest<HearingDto>;

public sealed class GetHearingQueryHandler(IHearingService hearingService, ICurrentUserService currentUser) : IRequestHandler<GetHearingQuery, HearingDto>
{
    public async Task<HearingDto> Handle(GetHearingQuery request, CancellationToken cancellationToken)
        => await hearingService.GetByIdAsync(currentUser.TenantId!.Value, request.HearingId, cancellationToken)
           ?? throw new NotFoundException("Hearing", request.HearingId);
}

public sealed record GetHearingsByCaseQuery(Guid CaseId) : IRequest<IReadOnlyList<HearingDto>>;

public sealed class GetHearingsByCaseQueryHandler(IHearingService hearingService, ICurrentUserService currentUser) : IRequestHandler<GetHearingsByCaseQuery, IReadOnlyList<HearingDto>>
{
    public Task<IReadOnlyList<HearingDto>> Handle(GetHearingsByCaseQuery request, CancellationToken cancellationToken)
        => hearingService.GetByCaseAsync(currentUser.TenantId!.Value, request.CaseId, cancellationToken);
}

/// <summary>GET /api/v1/hearings?date=&amp;courtId=&amp;lawyerId= (cause list). AC-CC2.</summary>
public sealed record GetCauseListQuery(DateOnly Date, Guid? CourtId, Guid? LawyerId) : IRequest<IReadOnlyList<HearingDto>>;

public sealed class GetCauseListQueryHandler(IHearingService hearingService, ICurrentUserService currentUser) : IRequestHandler<GetCauseListQuery, IReadOnlyList<HearingDto>>
{
    public Task<IReadOnlyList<HearingDto>> Handle(GetCauseListQuery request, CancellationToken cancellationToken)
        => hearingService.GetCauseListAsync(currentUser.TenantId!.Value, request.Date, request.CourtId, request.LawyerId, cancellationToken);
}
