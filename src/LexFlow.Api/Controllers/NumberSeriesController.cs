using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.NumberSeries;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.NumberSeries;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 15 §10 Number Series CRUD (PRD §17: "CRUD number-series, tax-rates, templates"). AC-S2: preview endpoint.</summary>
[ApiController]
[Route("api/v1/settings/number-series")]
public sealed class NumberSeriesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission("settings.read.all")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<NumberSeriesDto>>.Of(await mediator.Send(new GetNumberSeriesListQuery(), cancellationToken)));

    [HttpPost]
    [RequirePermission("settings.manage.all")]
    public async Task<IActionResult> Create([FromBody] CreateNumberSeriesCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<NumberSeriesDto>.Of(await mediator.Send(command, cancellationToken)));

    [HttpPut("{id:guid}")]
    [RequirePermission("settings.manage.all")]
    public async Task<IActionResult> UpdatePattern(Guid id, [FromBody] UpdateNumberSeriesPatternRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<NumberSeriesDto>.Of(await mediator.Send(new UpdateNumberSeriesPatternCommand(id, request.FormatPattern), cancellationToken)));

    [HttpDelete("{id:guid}")]
    [RequirePermission("settings.manage.all")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteNumberSeriesCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>AC-S2: this preview must match the actual next generated number.</summary>
    [HttpGet("{id:guid}/preview")]
    [RequirePermission("settings.read.all")]
    public async Task<IActionResult> PreviewNext(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<string>.Of(await mediator.Send(new PreviewNextNumberQuery(id), cancellationToken)));
}

public sealed record UpdateNumberSeriesPatternRequest(string FormatPattern);
