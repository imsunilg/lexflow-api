using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.TimeTracking;

public sealed record StartTimerCommand(Guid? MatterId, Guid? ActivityCodeId, string? ContextRef) : IRequest<RunningTimerDto>;

public sealed class StartTimerCommandHandler(ITimeTrackingService service, ICurrentUserService currentUser) : IRequestHandler<StartTimerCommand, RunningTimerDto>
{
    public Task<RunningTimerDto> Handle(StartTimerCommand request, CancellationToken cancellationToken)
        => service.StartTimerAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, new StartTimerInput(request.MatterId, request.ActivityCodeId, request.ContextRef), cancellationToken);
}

public sealed record PauseTimerCommand : IRequest<RunningTimerDto>;

public sealed class PauseTimerCommandHandler(ITimeTrackingService service, ICurrentUserService currentUser) : IRequestHandler<PauseTimerCommand, RunningTimerDto>
{
    public Task<RunningTimerDto> Handle(PauseTimerCommand request, CancellationToken cancellationToken)
        => service.PauseTimerAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, cancellationToken);
}

public sealed record ResumeTimerCommand : IRequest<RunningTimerDto>;

public sealed class ResumeTimerCommandHandler(ITimeTrackingService service, ICurrentUserService currentUser) : IRequestHandler<ResumeTimerCommand, RunningTimerDto>
{
    public Task<RunningTimerDto> Handle(ResumeTimerCommand request, CancellationToken cancellationToken)
        => service.ResumeTimerAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, cancellationToken);
}

public sealed record StopTimerCommand(bool Billable, string? Narrative, string? InternalNote, Guid? ActivityCodeId) : IRequest<TimeEntryDto>;

public sealed class StopTimerCommandHandler(ITimeTrackingService service, ICurrentUserService currentUser) : IRequestHandler<StopTimerCommand, TimeEntryDto>
{
    public Task<TimeEntryDto> Handle(StopTimerCommand request, CancellationToken cancellationToken)
        => service.StopTimerAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, new StopTimerInput(request.Billable, request.Narrative, request.InternalNote, request.ActivityCodeId), cancellationToken);
}

public sealed record CreateTimeEntryCommand(Guid MatterId, Guid? ActivityCodeId, DateOnly EntryDate, DateTimeOffset? StartedAt, int DurationMin, bool Billable, string? Narrative, string? InternalNote) : IRequest<TimeEntryDto>;

public sealed class CreateTimeEntryCommandHandler(ITimeTrackingService service, ICurrentUserService currentUser) : IRequestHandler<CreateTimeEntryCommand, TimeEntryDto>
{
    public Task<TimeEntryDto> Handle(CreateTimeEntryCommand request, CancellationToken cancellationToken)
        => service.CreateManualEntryAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, new CreateTimeEntryInput(request.MatterId, request.ActivityCodeId, request.EntryDate, request.StartedAt, request.DurationMin, request.Billable, request.Narrative, request.InternalNote), cancellationToken);
}

public sealed record UpdateTimeEntryCommand(Guid Id, Guid MatterId, Guid? ActivityCodeId, DateOnly EntryDate, int DurationMin, bool Billable, string? Narrative, string? InternalNote) : IRequest<TimeEntryDto>;

public sealed class UpdateTimeEntryCommandHandler(ITimeTrackingService service, ICurrentUserService currentUser) : IRequestHandler<UpdateTimeEntryCommand, TimeEntryDto>
{
    public Task<TimeEntryDto> Handle(UpdateTimeEntryCommand request, CancellationToken cancellationToken)
        => service.UpdateEntryAsync(currentUser.TenantId!.Value, request.Id, new UpdateTimeEntryInput(request.MatterId, request.ActivityCodeId, request.EntryDate, request.DurationMin, request.Billable, request.Narrative, request.InternalNote), cancellationToken);
}

public sealed record DeleteTimeEntryCommand(Guid Id) : IRequest;

public sealed class DeleteTimeEntryCommandHandler(ITimeTrackingService service, ICurrentUserService currentUser) : IRequestHandler<DeleteTimeEntryCommand>
{
    public async Task Handle(DeleteTimeEntryCommand request, CancellationToken cancellationToken)
        => await service.DeleteEntryAsync(currentUser.TenantId!.Value, request.Id, cancellationToken);
}

public sealed record SubmitTimeEntriesCommand(IReadOnlyList<Guid> Ids) : IRequest<IReadOnlyList<TimeEntryDto>>;

public sealed class SubmitTimeEntriesCommandHandler(ITimeTrackingService service, ICurrentUserService currentUser) : IRequestHandler<SubmitTimeEntriesCommand, IReadOnlyList<TimeEntryDto>>
{
    public Task<IReadOnlyList<TimeEntryDto>> Handle(SubmitTimeEntriesCommand request, CancellationToken cancellationToken)
        => service.SubmitAsync(currentUser.TenantId!.Value, request.Ids, cancellationToken);
}

public sealed record ApproveTimeEntriesCommand(IReadOnlyList<Guid> Ids, decimal? ManualRateOverride) : IRequest<IReadOnlyList<TimeEntryDto>>;

public sealed class ApproveTimeEntriesCommandHandler(ITimeTrackingService service, ICurrentUserService currentUser) : IRequestHandler<ApproveTimeEntriesCommand, IReadOnlyList<TimeEntryDto>>
{
    public Task<IReadOnlyList<TimeEntryDto>> Handle(ApproveTimeEntriesCommand request, CancellationToken cancellationToken)
        => service.ApproveAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.Ids, request.ManualRateOverride, cancellationToken);
}

public sealed record RejectTimeEntriesCommand(IReadOnlyList<Guid> Ids, string? Comment) : IRequest<IReadOnlyList<TimeEntryDto>>;

public sealed class RejectTimeEntriesCommandHandler(ITimeTrackingService service, ICurrentUserService currentUser) : IRequestHandler<RejectTimeEntriesCommand, IReadOnlyList<TimeEntryDto>>
{
    public Task<IReadOnlyList<TimeEntryDto>> Handle(RejectTimeEntriesCommand request, CancellationToken cancellationToken)
        => service.RejectAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.Ids, request.Comment, cancellationToken);
}
