using LexFlow.Api.Contracts;
using LexFlow.Api.Security;
using LexFlow.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers.Portal;

/// <summary>Module 17: GET /me/matters, GET /matters/{id}/timeline. client_id always comes from the token (ICurrentPortalUserService), never the route.</summary>
[ApiController]
[Route("api/portal/v1")]
[EnableCors("LexFlowPortal")]
[Authorize(AuthenticationSchemes = PortalAuthenticationDefaults.AuthenticationScheme)]
public sealed class PortalMattersController(IPortalTimelineService timelineService, IPortalScopeService scopeService, ICurrentPortalUserService currentPortalUser) : ControllerBase
{
    [HttpGet("me/matters")]
    public async Task<IActionResult> GetMyMatters(CancellationToken cancellationToken)
    {
        var (tenantId, portalUserId, clientId) = RequireIdentity();
        var visibleMatterIds = await scopeService.GetVisibleMatterIdsAsync(tenantId, portalUserId, cancellationToken);
        var matters = await timelineService.GetMyMattersAsync(tenantId, clientId, visibleMatterIds, cancellationToken);
        return Ok(ApiResponse<object>.Of(matters));
    }

    [HttpGet("matters/{matterId:guid}/timeline")]
    public async Task<IActionResult> GetTimeline(Guid matterId, CancellationToken cancellationToken)
    {
        var (tenantId, portalUserId, clientId) = RequireIdentity();
        var visibleMatterIds = await scopeService.GetVisibleMatterIdsAsync(tenantId, portalUserId, cancellationToken);
        var timeline = await timelineService.GetTimelineAsync(tenantId, clientId, visibleMatterIds, matterId, cancellationToken);
        return Ok(ApiResponse<object>.Of(timeline));
    }

    private (Guid TenantId, Guid PortalUserId, Guid ClientId) RequireIdentity()
    {
        if (currentPortalUser.TenantId is not { } tenantId || currentPortalUser.PortalUserId is not { } portalUserId || currentPortalUser.ClientId is not { } clientId)
        {
            throw new UnauthorizedAccessException("No authenticated portal user.");
        }

        return (tenantId, portalUserId, clientId);
    }
}
