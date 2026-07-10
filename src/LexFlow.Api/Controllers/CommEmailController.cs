using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Comm;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Comm;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 11 email — PRD §17.</summary>
[ApiController]
[Route("api/v1/comm/email")]
public sealed class CommEmailController(IMediator mediator) : ControllerBase
{
    [HttpPost("accounts/connect")]
    [RequirePermission("comm.email.manage")]
    public async Task<IActionResult> Connect([FromBody] ConnectEmailAccountRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<string>.Of(await mediator.Send(new ConnectEmailAccountCommand(request.Provider, request.RedirectUri), cancellationToken)));

    [HttpPost("accounts/callback")]
    [RequirePermission("comm.email.manage")]
    public async Task<IActionResult> OAuthCallback([FromBody] EmailOAuthCallbackRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<Guid>.Of(await mediator.Send(new HandleEmailOAuthCallbackCommand(request.Provider, request.Code, request.RedirectUri), cancellationToken)));

    [HttpGet("threads")]
    [RequirePermission("comm.email.read")]
    public async Task<IActionResult> GetThreads([FromQuery] Guid? matterId, [FromQuery] Guid? clientId, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<EmailThreadDto>>.Of(await mediator.Send(new GetEmailThreadsQuery(matterId, clientId), cancellationToken)));

    /// <summary>AC-CM3: threads awaiting manual matter/client confirmation.</summary>
    [HttpGet("threads/triage")]
    [RequirePermission("comm.email.read")]
    public async Task<IActionResult> GetTriageQueue(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<EmailThreadDto>>.Of(await mediator.Send(new GetEmailTriageQueueQuery(), cancellationToken)));

    [HttpGet("threads/{id:guid}/messages")]
    [RequirePermission("comm.email.read")]
    public async Task<IActionResult> GetMessages(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<EmailMessageDto>>.Of(await mediator.Send(new GetEmailMessagesQuery(id), cancellationToken)));

    [HttpPost("threads/{id:guid}/link")]
    [RequirePermission("comm.email.manage")]
    public async Task<IActionResult> LinkThread(Guid id, [FromBody] LinkEmailThreadRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<EmailThreadDto>.Of(await mediator.Send(new LinkEmailThreadCommand(id, request.MatterId, request.ClientId), cancellationToken)));

    [HttpPost("send")]
    [RequirePermission("comm.email.manage")]
    public async Task<IActionResult> Send([FromBody] SendEmailRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<EmailThreadDto>.Of(await mediator.Send(new SendEmailCommand(request.MailboxId, request.ToAddresses, request.Subject, request.BodyHtml, request.Attachments, request.InReplyToMessageIdHdr, request.MatterId, request.ClientId), cancellationToken)));
}

public sealed record ConnectEmailAccountRequest(string Provider, string RedirectUri);

public sealed record EmailOAuthCallbackRequest(string Provider, string Code, string RedirectUri);

public sealed record LinkEmailThreadRequest(Guid MatterId, Guid? ClientId);

public sealed record SendEmailRequest(Guid MailboxId, IReadOnlyList<string> ToAddresses, string Subject, string BodyHtml, IReadOnlyList<EmailAttachmentInput>? Attachments, string? InReplyToMessageIdHdr, Guid? MatterId, Guid? ClientId);
