using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Comm;

/// <summary>GET /api/v1/chat/channels.</summary>
public sealed record GetChatChannelsQuery : IRequest<IReadOnlyList<ChatChannelDto>>;

public sealed class GetChatChannelsQueryHandler(IChatService chatService, ICurrentUserService currentUser) : IRequestHandler<GetChatChannelsQuery, IReadOnlyList<ChatChannelDto>>
{
    public Task<IReadOnlyList<ChatChannelDto>> Handle(GetChatChannelsQuery request, CancellationToken cancellationToken)
        => chatService.GetChannelsForUserAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, cancellationToken);
}

/// <summary>GET /api/v1/chat/channels/{id}/messages.</summary>
public sealed record GetChatMessagesQuery(Guid ChannelId, long? AfterSeq, int Limit) : IRequest<IReadOnlyList<ChatMessageDto>>;

public sealed class GetChatMessagesQueryHandler(IChatService chatService, ICurrentUserService currentUser) : IRequestHandler<GetChatMessagesQuery, IReadOnlyList<ChatMessageDto>>
{
    public Task<IReadOnlyList<ChatMessageDto>> Handle(GetChatMessagesQuery request, CancellationToken cancellationToken)
        => chatService.GetMessagesAsync(currentUser.TenantId!.Value, request.ChannelId, request.AfterSeq, request.Limit, cancellationToken);
}
