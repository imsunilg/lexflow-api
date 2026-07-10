using LexFlow.Api.Contracts;
using LexFlow.Api.Contracts.Portal;
using LexFlow.Api.Security;
using LexFlow.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers.Portal;

/// <summary>Module 17 User Flow #7: secure per-matter threaded messaging with the firm team.</summary>
[ApiController]
[Route("api/portal/v1")]
[EnableCors("LexFlowPortal")]
[Authorize(AuthenticationSchemes = PortalAuthenticationDefaults.AuthenticationScheme)]
public sealed class PortalMessagesController(IPortalMessagingService messagingService, IPortalScopeService scopeService, ICurrentPortalUserService currentPortalUser) : ControllerBase
{
    [HttpGet("threads")]
    public async Task<IActionResult> GetThreads(CancellationToken cancellationToken)
    {
        var (tenantId, portalUserId, clientId) = RequireIdentity();
        var visibleMatterIds = await scopeService.GetVisibleMatterIdsAsync(tenantId, portalUserId, cancellationToken);
        var threads = await messagingService.GetThreadsAsync(tenantId, clientId, visibleMatterIds, cancellationToken);
        return Ok(ApiResponse<object>.Of(threads));
    }

    [HttpGet("threads/{threadId:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid threadId, CancellationToken cancellationToken)
    {
        var (tenantId, portalUserId, clientId) = RequireIdentity();
        var visibleMatterIds = await scopeService.GetVisibleMatterIdsAsync(tenantId, portalUserId, cancellationToken);
        var messages = await messagingService.GetMessagesAsync(tenantId, clientId, visibleMatterIds, threadId, cancellationToken);
        return Ok(ApiResponse<object>.Of(messages));
    }

    [HttpPost("threads/{threadId:guid}/messages")]
    public async Task<IActionResult> PostMessage(Guid threadId, [FromBody] PortalPostMessageRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, portalUserId, clientId) = RequireIdentity();
        var visibleMatterIds = await scopeService.GetVisibleMatterIdsAsync(tenantId, portalUserId, cancellationToken);
        var message = await messagingService.PostMessageAsync(tenantId, portalUserId, clientId, visibleMatterIds, threadId, request.Body, cancellationToken);
        return Ok(ApiResponse<object>.Of(message));
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
