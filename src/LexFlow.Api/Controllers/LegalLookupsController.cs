using LexFlow.Api.Contracts;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Matters;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Read-only dropdown data (courts/judges/practice areas) for the matter/case-create forms — see ILegalLookupService for why these have no dedicated CRUD in Module 4/5's API list.</summary>
[ApiController]
[Route("api/v1/legal-lookups")]
public sealed class LegalLookupsController(IMediator mediator) : ControllerBase
{
    [HttpGet("courts")]
    [RequirePermission("matters.read.all")]
    public async Task<IActionResult> GetCourts(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<CourtDto>>.Of(await mediator.Send(new GetCourtsQuery(), cancellationToken)));

    [HttpGet("judges")]
    [RequirePermission("matters.read.all")]
    public async Task<IActionResult> GetJudges([FromQuery] Guid? courtId, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<JudgeDto>>.Of(await mediator.Send(new GetJudgesQuery(courtId), cancellationToken)));

    [HttpGet("practice-areas")]
    [RequirePermission("matters.read.all")]
    public async Task<IActionResult> GetPracticeAreas(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<PracticeAreaDto>>.Of(await mediator.Send(new GetPracticeAreasQuery(), cancellationToken)));
}
