using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Kb;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Kb;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 12 pin-to-matter — PRD §17 `POST /api/v1/matters/{id}/kb-pins`. AC-KB4: pin note + back-link "pinned in N matters"; Security Rules: "matter pins follow matter ACL" (RequirePermission below gates on the caller's matter-read permission, same gate as the matter's own Research/Arguments tab).</summary>
[ApiController]
[Route("api/v1/matters/{matterId:guid}/kb-pins")]
public sealed class KbMatterPinsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [RequirePermission("matters.read.all")]
    public async Task<IActionResult> Pin(Guid matterId, [FromBody] PinKbItemRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<KbMatterPinDto>.Of(await mediator.Send(new PinKbItemToMatterCommand(matterId, request.KbRefKind, request.KbRefId, request.Note), cancellationToken)));

    [HttpGet]
    [RequirePermission("matters.read.all")]
    public async Task<IActionResult> GetForMatter(Guid matterId, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<KbMatterPinDto>>.Of(await mediator.Send(new GetKbMatterPinsQuery(matterId), cancellationToken)));

    [HttpDelete("{pinId:guid}")]
    [RequirePermission("matters.read.all")]
    public async Task<IActionResult> Unpin(Guid matterId, Guid pinId, CancellationToken cancellationToken)
    {
        await mediator.Send(new UnpinKbItemFromMatterCommand(pinId), cancellationToken);
        return NoContent();
    }
}

public sealed record PinKbItemRequest(string KbRefKind, Guid KbRefId, string? Note);
