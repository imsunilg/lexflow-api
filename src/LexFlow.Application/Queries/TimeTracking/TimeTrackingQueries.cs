using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.TimeTracking;

public sealed record GetCurrentTimerQuery : IRequest<RunningTimerDto?>;

public sealed class GetCurrentTimerQueryHandler(ITimeTrackingService service, ICurrentUserService currentUser) : IRequestHandler<GetCurrentTimerQuery, RunningTimerDto?>
{
    public Task<RunningTimerDto?> Handle(GetCurrentTimerQuery request, CancellationToken cancellationToken)
        => service.GetCurrentTimerAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, cancellationToken);
}

public sealed record GetTimeEntryQuery(Guid Id) : IRequest<TimeEntryDto?>;

public sealed class GetTimeEntryQueryHandler(ITimeTrackingService service, ICurrentUserService currentUser) : IRequestHandler<GetTimeEntryQuery, TimeEntryDto?>
{
    public Task<TimeEntryDto?> Handle(GetTimeEntryQuery request, CancellationToken cancellationToken)
        => service.GetEntryAsync(currentUser.TenantId!.Value, request.Id, cancellationToken);
}

public sealed record GetTimeEntriesQuery(Guid? UserId, Guid? MatterId, string? Status, DateOnly? From, DateOnly? To) : IRequest<IReadOnlyList<TimeEntryDto>>;

public sealed class GetTimeEntriesQueryHandler(ITimeTrackingService service, ICurrentUserService currentUser) : IRequestHandler<GetTimeEntriesQuery, IReadOnlyList<TimeEntryDto>>
{
    public Task<IReadOnlyList<TimeEntryDto>> Handle(GetTimeEntriesQuery request, CancellationToken cancellationToken)
        => service.GetEntriesAsync(currentUser.TenantId!.Value, new TimeEntryFilter(request.UserId, request.MatterId, request.Status, request.From, request.To), cancellationToken);
}
