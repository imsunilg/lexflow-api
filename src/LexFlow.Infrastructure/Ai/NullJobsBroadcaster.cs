using LexFlow.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace LexFlow.Infrastructure.Ai;

/// <summary>
/// Default IJobsBroadcaster registration for processes that can't reference the SignalR Hub type
/// (LexFlow.Workers — same dependency-direction constraint as ChatHub/IChatBroadcaster). The Api
/// project overrides this with the real SignalR-backed SignalRJobsBroadcaster after calling
/// AddInfrastructure (last registration wins), so a Hangfire job running in Workers — which has
/// no SignalR hub to push to anyway — logs instead of throwing a missing-dependency error, while
/// the API process's own in-request notify path still gets the real push.
/// </summary>
public sealed class NullJobsBroadcaster(ILogger<NullJobsBroadcaster> logger) : IJobsBroadcaster
{
    public Task NotifyJobCompletedAsync(Guid tenantId, Guid userId, string jobKind, Guid jobId, string status, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Job {JobKind} {JobId} completed with status {Status} for user {UserId} (no SignalR broadcaster registered in this process).", jobKind, jobId, status, userId);
        return Task.CompletedTask;
    }
}
