using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.TaxRates;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.TaxRates;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 15 §8 Taxes CRUD (PRD §17: "CRUD number-series, tax-rates, templates").</summary>
[ApiController]
[Route("api/v1/settings/tax-rates")]
public sealed class TaxRatesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission("settings.read.all")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<TaxRateDto>>.Of(await mediator.Send(new GetTaxRatesQuery(), cancellationToken)));

    [HttpPost]
    [RequirePermission("settings.manage.all")]
    public async Task<IActionResult> Create([FromBody] CreateTaxRateCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<TaxRateDto>.Of(await mediator.Send(command, cancellationToken)));

    [HttpPut("{id:guid}")]
    [RequirePermission("settings.manage.all")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTaxRateRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<TaxRateDto>.Of(await mediator.Send(
            new UpdateTaxRateCommand(id, request.CountryCode, request.TaxType, request.ComponentsJson, request.IsActive), cancellationToken)));

    [HttpDelete("{id:guid}")]
    [RequirePermission("settings.manage.all")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteTaxRateCommand(id), cancellationToken);
        return NoContent();
    }
}

public sealed record UpdateTaxRateRequest(string CountryCode, string TaxType, string ComponentsJson, bool IsActive);
