using LexFlow.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace LexFlow.Api.Hubs;

/// <summary>The real /hubs/jobs implementation of IJobsBroadcaster — see that interface's doc comment for why this lives in Api. Module 16: "the Whisper-class transcription pipeline (async, Hangfire job, webhook/SignalR notify on completion)."</summary>
public sealed class SignalRJobsBroadcaster(IHubContext<JobsHub> hubContext) : IJobsBroadcaster
{
    public Task NotifyJobCompletedAsync(Guid tenantId, Guid userId, string jobKind, Guid jobId, string status, CancellationToken cancellationToken = default)
        => hubContext.Clients.Group(JobsHub.UserGroupName(userId.ToString())).SendAsync("jobCompleted", new { jobKind, jobId, status }, cancellationToken);
}
