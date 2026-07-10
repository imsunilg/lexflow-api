using LexFlow.Api.Contracts;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Kb;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 12 KB search — PRD §17. AC-KB1 (section jump &lt; 500ms) / AC-KB2 (full-text incl. OCR).</summary>
[ApiController]
[Route("api/v1/kb")]
public sealed class KbSearchController(IMediator mediator) : ControllerBase
{
    [HttpGet("search")]
    [RequirePermission("kb.read.all")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] string? type, [FromQuery] Guid? courtId, [FromQuery] int? yearFrom, [FromQuery] string? tag, CancellationToken cancellationToken)
        => Ok(ApiResponse<KbSearchResult>.Of(await mediator.Send(new KbSearchAppQuery(q, type, courtId, yearFrom, tag), cancellationToken)));
}
