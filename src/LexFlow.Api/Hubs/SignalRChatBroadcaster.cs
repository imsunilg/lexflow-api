using LexFlow.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace LexFlow.Api.Hubs;

/// <summary>The real /hubs/chat implementation of IChatBroadcaster — see that interface's doc comment for why this lives in Api (the only project that can reference ChatHub without inverting the dependency direction).</summary>
public sealed class SignalRChatBroadcaster(IHubContext<ChatHub> hubContext) : IChatBroadcaster
{
    public Task BroadcastMessageAsync(Guid tenantId, Guid channelId, ChatMessageDto message, CancellationToken cancellationToken = default)
        => hubContext.Clients.Group(ChatHub.ChannelGroupName(channelId.ToString())).SendAsync("messageReceived", message, cancellationToken);
}
