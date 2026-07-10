using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Comm;

/// <summary>POST /api/v1/comm/calls.</summary>
public sealed record LogCallCommand(Guid? ClientId, Guid? MatterId, Guid? UserId, string Direction, int DurationSec, string? Summary, bool CreateFollowUpTask, string? FollowUpTaskTitle) : IRequest<CallLogDto>;

public sealed class LogCallCommandHandler(ICallService callService, ICurrentUserService currentUser) : IRequestHandler<LogCallCommand, CallLogDto>
{
    public Task<CallLogDto> Handle(LogCallCommand request, CancellationToken cancellationToken)
        => callService.LogAsync(currentUser.TenantId!.Value, currentUser.UserId, new LogCallInput(request.ClientId, request.MatterId, request.UserId, request.Direction, request.DurationSec, request.Summary, request.CreateFollowUpTask, request.FollowUpTaskTitle), cancellationToken);
}

/// <summary>POST /api/v1/comm/calls/click-to-call.</summary>
public sealed record ClickToCallCommand(Guid? ClientId, Guid? MatterId, string ToNumber, bool ConsentGiven) : IRequest<CallLogDto>;

public sealed class ClickToCallCommandHandler(ICallService callService, ICurrentUserService currentUser) : IRequestHandler<ClickToCallCommand, CallLogDto>
{
    public Task<CallLogDto> Handle(ClickToCallCommand request, CancellationToken cancellationToken)
        => callService.ClickToCallAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, new ClickToCallInput(request.ClientId, request.MatterId, request.ToNumber, request.ConsentGiven), cancellationToken);
}
