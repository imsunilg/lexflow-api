using LexFlow.Api.Contracts;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Roles;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>GET /api/v1/permissions/catalog (PRD §17, §21) — server-served so UI always renders the current catalog dynamically.</summary>
[ApiController]
[Route("api/v1/permissions")]
public sealed class PermissionsController(IMediator mediator) : ControllerBase
{
    [HttpGet("catalog")]
    [RequirePermission("users.read.all")]
    public async Task<IActionResult> GetCatalog(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<PermissionCatalogItem>>.Of(await mediator.Send(new GetPermissionsCatalogQuery(), cancellationToken)));
}
