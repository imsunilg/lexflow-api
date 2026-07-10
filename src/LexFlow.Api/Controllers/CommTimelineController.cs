using LexFlow.Api.Contracts;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Comm;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 11: "Unified client Communication tab" — PRD §17: GET /api/v1/comm/timeline?clientId=&amp;channels=.</summary>
[ApiController]
[Route("api/v1/comm/timeline")]
public sealed class CommTimelineController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission("comm.timeline.read")]
    public async Task<IActionResult> Get([FromQuery] Guid? clientId, [FromQuery] string? channels, CancellationToken cancellationToken)
    {
        var channelList = string.IsNullOrWhiteSpace(channels) ? null : channels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Ok(ApiResponse<IReadOnlyList<CommTimelineEntryDto>>.Of(await mediator.Send(new GetCommTimelineQuery(clientId, channelList), cancellationToken)));
    }
}
