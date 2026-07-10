using LexFlow.Api.Contracts;
using LexFlow.Api.Contracts.Portal;
using LexFlow.Api.Security;
using LexFlow.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers.Portal;

/// <summary>Module 17 User Flow #6: appointment requests.</summary>
[ApiController]
[Route("api/portal/v1/appointments")]
[EnableCors("LexFlowPortal")]
[Authorize(AuthenticationSchemes = PortalAuthenticationDefaults.AuthenticationScheme)]
public sealed class PortalAppointmentsController(IPortalAppointmentService appointmentService, IPortalScopeService scopeService, ICurrentPortalUserService currentPortalUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAppointments(CancellationToken cancellationToken)
    {
        var (_, _, clientId) = RequireIdentity();
        var appointments = await appointmentService.GetAppointmentsAsync(RequireTenantId(), clientId, cancellationToken);
        return Ok(ApiResponse<object>.Of(appointments));
    }

    [HttpPost]
    public async Task<IActionResult> RequestAppointment([FromBody] PortalAppointmentRequestDto request, CancellationToken cancellationToken)
    {
        var (tenantId, portalUserId, clientId) = RequireIdentity();
        var visibleMatterIds = await scopeService.GetVisibleMatterIdsAsync(tenantId, portalUserId, cancellationToken);
        var appointment = await appointmentService.RequestAppointmentAsync(
            tenantId, portalUserId, clientId, visibleMatterIds, request.MatterId, request.LawyerId, request.RequestedStart, request.RequestedEnd, request.Notes, cancellationToken);
        return Ok(ApiResponse<object>.Of(appointment));
    }

    private Guid RequireTenantId() => currentPortalUser.TenantId ?? throw new UnauthorizedAccessException("No authenticated portal user.");

    private (Guid TenantId, Guid PortalUserId, Guid ClientId) RequireIdentity()
    {
        if (currentPortalUser.TenantId is not { } tenantId || currentPortalUser.PortalUserId is not { } portalUserId || currentPortalUser.ClientId is not { } clientId)
        {
            throw new UnauthorizedAccessException("No authenticated portal user.");
        }

        return (tenantId, portalUserId, clientId);
    }
}
