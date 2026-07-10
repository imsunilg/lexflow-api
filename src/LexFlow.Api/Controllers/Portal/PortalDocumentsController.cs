using LexFlow.Api.Contracts;
using LexFlow.Api.Contracts.Portal;
using LexFlow.Api.Security;
using LexFlow.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers.Portal;

/// <summary>Module 17 User Flow #5: published-to-portal documents (download) + client upload-to-firm.</summary>
[ApiController]
[Route("api/portal/v1/documents")]
[EnableCors("LexFlowPortal")]
[Authorize(AuthenticationSchemes = PortalAuthenticationDefaults.AuthenticationScheme)]
public sealed class PortalDocumentsController(IPortalDocumentService documentService, IPortalScopeService scopeService, ICurrentPortalUserService currentPortalUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetDocuments([FromQuery] Guid? matterId, CancellationToken cancellationToken)
    {
        var (tenantId, _, clientId) = RequireIdentity();
        var visibleMatterIds = await scopeService.GetVisibleMatterIdsAsync(tenantId, currentPortalUser.PortalUserId!.Value, cancellationToken);
        var documents = await documentService.GetDocumentsAsync(tenantId, clientId, visibleMatterIds, matterId, cancellationToken);
        return Ok(ApiResponse<object>.Of(documents));
    }

    [HttpGet("{documentId:guid}/download")]
    public async Task<IActionResult> Download(Guid documentId, CancellationToken cancellationToken)
    {
        var (tenantId, _, clientId) = RequireIdentity();
        var visibleMatterIds = await scopeService.GetVisibleMatterIdsAsync(tenantId, currentPortalUser.PortalUserId!.Value, cancellationToken);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        var url = await documentService.GetDownloadUrlAsync(tenantId, clientId, visibleMatterIds, documentId, ip, userAgent, cancellationToken);
        return Ok(ApiResponse<object>.Of(new { url }));
    }

    [HttpPost]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> Upload([FromForm] PortalUploadDocumentRequest metadata, IFormFile file, CancellationToken cancellationToken)
    {
        var (tenantId, portalUserId, clientId) = RequireIdentity();
        var visibleMatterIds = await scopeService.GetVisibleMatterIdsAsync(tenantId, portalUserId, cancellationToken);

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);

        var document = await documentService.UploadAsync(tenantId, portalUserId, clientId, visibleMatterIds, metadata.MatterId, stream.ToArray(), file.FileName, file.ContentType, metadata.Title, cancellationToken);
        return Ok(ApiResponse<object>.Of(document));
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
