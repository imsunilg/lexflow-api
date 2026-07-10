using Microsoft.AspNetCore.SignalR;

namespace LexFlow.Api.Hubs;

/// <summary>/hubs/jobs (PRD §16 Realtime) — background job progress (OCR, imports, AI jobs). Module 16: the transcription pipeline's completion notify is the first real consumer. Clients join their own per-user group so a job result reaches only the user who requested it.</summary>
public sealed class JobsHub : Hub
{
    public Task JoinUserGroup(string userId) => Groups.AddToGroupAsync(Context.ConnectionId, UserGroupName(userId));

    public Task LeaveUserGroup(string userId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, UserGroupName(userId));

    internal static string UserGroupName(string userId) => $"jobs-user-{userId}";
}
