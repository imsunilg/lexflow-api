using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Comm;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Comm;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 11 WhatsApp — PRD §17. AC-CM2.</summary>
[ApiController]
[Route("api/v1/comm/whatsapp")]
public sealed class CommWhatsAppController(IMediator mediator) : ControllerBase
{
    [HttpPost("send")]
    [RequirePermission("comm.whatsapp.manage")]
    public async Task<IActionResult> Send([FromBody] SendWhatsAppRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<WhatsappMessageDto>.Of(await mediator.Send(new SendWhatsAppCommand(request.ClientId, request.TemplateId, request.Variables, request.SessionText), cancellationToken)));

    [HttpGet("clients/{clientId:guid}")]
    [RequirePermission("comm.whatsapp.read")]
    public async Task<IActionResult> GetForClient(Guid clientId, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<WhatsappMessageDto>>.Of(await mediator.Send(new GetWhatsAppForClientQuery(clientId), cancellationToken)));

    [HttpPost("clients/{clientId:guid}/opt-in")]
    [RequirePermission("comm.whatsapp.manage")]
    public async Task<IActionResult> OptIn(Guid clientId, [FromBody] OptInWhatsAppRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<WhatsAppOptinDto>.Of(await mediator.Send(new OptInWhatsAppCommand(clientId, request.PhoneE164, request.Source), cancellationToken)));

    [HttpPost("clients/{clientId:guid}/opt-out")]
    [RequirePermission("comm.whatsapp.manage")]
    public async Task<IActionResult> OptOut(Guid clientId, CancellationToken cancellationToken)
    {
        await mediator.Send(new OptOutWhatsAppCommand(clientId), cancellationToken);
        return NoContent();
    }
}

public sealed record SendWhatsAppRequest(Guid ClientId, Guid? TemplateId, IReadOnlyDictionary<string, string>? Variables, string? SessionText);

public sealed record OptInWhatsAppRequest(string PhoneE164, string? Source);
