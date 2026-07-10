using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Comm;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Comm;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 11 SMS — PRD §17. AC-CM4 (DLT enforcement) is enforced in ISmsService.</summary>
[ApiController]
[Route("api/v1/comm/sms")]
public sealed class CommSmsController(IMediator mediator) : ControllerBase
{
    [HttpPost("send")]
    [RequirePermission("comm.sms.manage")]
    public async Task<IActionResult> Send([FromBody] SendSmsRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<SmsMessageDto>.Of(await mediator.Send(new SendSmsCommand(request.ClientId, request.MatterId, request.ToNumber, request.TemplateId, request.FreeformBody, request.Variables), cancellationToken)));

    [HttpGet("clients/{clientId:guid}")]
    [RequirePermission("comm.sms.read")]
    public async Task<IActionResult> GetForClient(Guid clientId, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<SmsMessageDto>>.Of(await mediator.Send(new GetSmsForClientQuery(clientId), cancellationToken)));
}

public sealed record SendSmsRequest(Guid? ClientId, Guid? MatterId, string ToNumber, Guid? TemplateId, string? FreeformBody, IReadOnlyDictionary<string, string>? Variables);
