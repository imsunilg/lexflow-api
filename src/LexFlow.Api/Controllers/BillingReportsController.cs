using LexFlow.Api.Contracts;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Billing;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 8: GET /api/v1/billing/aging?bucket= — AC-B6.</summary>
[ApiController]
[Route("api/v1/billing")]
public sealed class BillingReportsController(IMediator mediator) : ControllerBase
{
    [HttpGet("aging")]
    [RequirePermission("invoices.read.all")]
    public async Task<IActionResult> GetAging([FromQuery] DateOnly? asOf, CancellationToken cancellationToken)
        => Ok(ApiResponse<AgingReportDto>.Of(await mediator.Send(new GetAgingReportQuery(asOf ?? DateOnly.FromDateTime(DateTime.UtcNow)), cancellationToken)));
}
