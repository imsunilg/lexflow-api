using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Comm;

/// <summary>POST /api/v1/chat/channels.</summary>
public sealed record CreateChatChannelCommand(string Kind, string? Name, Guid? MatterId, Guid? TeamId, IReadOnlyList<Guid> MemberUserIds) : IRequest<ChatChannelDto>;

public sealed class CreateChatChannelCommandHandler(IChatService chatService, ICurrentUserService currentUser) : IRequestHandler<CreateChatChannelCommand, ChatChannelDto>
{
    public Task<ChatChannelDto> Handle(CreateChatChannelCommand request, CancellationToken cancellationToken)
        => chatService.CreateChannelAsync(currentUser.TenantId!.Value, request.Kind, request.Name, request.MatterId, request.TeamId, request.MemberUserIds, cancellationToken);
}

/// <summary>POST /api/v1/chat/channels/{id}/messages. AC-CM5.</summary>
public sealed record PostChatMessageCommand(Guid ChannelId, string Body) : IRequest<ChatMessageDto>;

public sealed class PostChatMessageCommandHandler(IChatService chatService, ICurrentUserService currentUser) : IRequestHandler<PostChatMessageCommand, ChatMessageDto>
{
    public Task<ChatMessageDto> Handle(PostChatMessageCommand request, CancellationToken cancellationToken)
        => chatService.PostMessageAsync(currentUser.TenantId!.Value, request.ChannelId, currentUser.UserId!.Value, request.Body, cancellationToken);
}

/// <summary>Module 11: "message -> task conversion."</summary>
public sealed record ConvertChatMessageToTaskCommand(Guid MessageId, string Title, DateTimeOffset? DueAt) : IRequest<Guid>;

public sealed class ConvertChatMessageToTaskCommandHandler(IChatService chatService, ICurrentUserService currentUser) : IRequestHandler<ConvertChatMessageToTaskCommand, Guid>
{
    public Task<Guid> Handle(ConvertChatMessageToTaskCommand request, CancellationToken cancellationToken)
        => chatService.ConvertMessageToTaskAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.MessageId, request.Title, request.DueAt, cancellationToken);
}
