using Microsoft.AspNetCore.SignalR;

namespace LexFlow.Api.Hubs;

/// <summary>/hubs/chat (PRD §16 Realtime, Module 11 internal chat). Clients join a group per channel id they're a member of; SignalRChatBroadcaster pushes new messages to that group.</summary>
public sealed class ChatHub : Hub
{
    public Task JoinChannel(string channelId) => Groups.AddToGroupAsync(Context.ConnectionId, ChannelGroupName(channelId));

    public Task LeaveChannel(string channelId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, ChannelGroupName(channelId));

    internal static string ChannelGroupName(string channelId) => $"chat-channel-{channelId}";
}
