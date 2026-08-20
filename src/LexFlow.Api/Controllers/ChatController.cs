using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Comm;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Comm;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 11 internal chat — PRD §17 (+SignalR hub /hubs/chat). AC-CM5.</summary>
[ApiController]
[Route("api/v1/chat/channels")]
public sealed class ChatController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [RequirePermission("comm.chat_manage.all")]
    public async Task<IActionResult> Create([FromBody] CreateChatChannelRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<ChatChannelDto>.Of(await mediator.Send(new CreateChatChannelCommand(request.Kind, request.Name, request.MatterId, request.TeamId, request.MemberUserIds), cancellationToken)));

    [HttpGet]
    [RequirePermission("comm.chat_read.all")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<ChatChannelDto>>.Of(await mediator.Send(new GetChatChannelsQuery(), cancellationToken)));

    [HttpPost("{id:guid}/messages")]
    [RequirePermission("comm.chat_manage.all")]
    public async Task<IActionResult> PostMessage(Guid id, [FromBody] PostChatMessageRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<ChatMessageDto>.Of(await mediator.Send(new PostChatMessageCommand(id, request.Body), cancellationToken)));

    [HttpGet("{id:guid}/messages")]
    [RequirePermission("comm.chat_read.all")]
    public async Task<IActionResult> GetMessages(Guid id, [FromQuery] long? afterSeq, [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
        => Ok(ApiResponse<IReadOnlyList<ChatMessageDto>>.Of(await mediator.Send(new GetChatMessagesQuery(id, afterSeq, limit), cancellationToken)));

    /// <summary>Module 11: "message -> task conversion."</summary>
    [HttpPost("messages/{messageId:guid}/convert-to-task")]
    [RequirePermission("comm.chat_manage.all")]
    public async Task<IActionResult> ConvertToTask(Guid messageId, [FromBody] ConvertChatMessageToTaskRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<Guid>.Of(await mediator.Send(new ConvertChatMessageToTaskCommand(messageId, request.Title, request.DueAt), cancellationToken)));
}

public sealed record CreateChatChannelRequest(string Kind, string? Name, Guid? MatterId, Guid? TeamId, IReadOnlyList<Guid> MemberUserIds);

public sealed record PostChatMessageRequest(string Body);

public sealed record ConvertChatMessageToTaskRequest(string Title, DateTimeOffset? DueAt);
