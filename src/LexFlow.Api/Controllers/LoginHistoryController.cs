using LexFlow.Api.Contracts;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Sessions;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>GET /api/v1/login-history?userId=&amp;from= (PRD §17, §20(12) login history browser).</summary>
[ApiController]
[Route("api/v1/login-history")]
public sealed class LoginHistoryController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission("audit.read.own")]
    public async Task<IActionResult> Get([FromQuery] Guid? userId, [FromQuery] DateTimeOffset? from, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<LoginHistoryDto>>.Of(await mediator.Send(new GetLoginHistoryQuery(userId, from), cancellationToken)));
}
