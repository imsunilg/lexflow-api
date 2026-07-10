using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.RateCards;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.RateCards;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 8: rate cards/entries (BR-7) + billing arrangements CRUD.</summary>
[ApiController]
[Route("api/v1/rate-cards")]
public sealed class RateCardsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [RequirePermission("rates.manage.all")]
    public async Task<IActionResult> Create([FromBody] CreateRateCardRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<RateCardDto>.Of(await mediator.Send(new CreateRateCardCommand(request.Name, request.BranchId, request.IsDefault), cancellationToken)));

    [HttpGet]
    [RequirePermission("rates.read.all")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<RateCardDto>>.Of(await mediator.Send(new GetRateCardsQuery(), cancellationToken)));

    [HttpPost("{id:guid}/entries")]
    [RequirePermission("rates.manage.all")]
    public async Task<IActionResult> UpsertEntry(Guid id, [FromBody] UpsertRateCardEntryRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<RateCardEntryDto>.Of(await mediator.Send(new UpsertRateCardEntryCommand(id, request.Role, request.UserId, request.Rate, request.Currency, request.EffectiveFrom), cancellationToken)));
}

/// <summary>Module 8: PUT-style upsert of the active billing arrangement for a matter.</summary>
[ApiController]
[Route("api/v1/matters/{matterId:guid}/billing-arrangement")]
public sealed class BillingArrangementsController(IMediator mediator) : ControllerBase
{
    [HttpPut]
    [RequirePermission("rates.manage.all")]
    public async Task<IActionResult> Set(Guid matterId, [FromBody] SetBillingArrangementRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<BillingArrangementDto>.Of(await mediator.Send(new SetBillingArrangementCommand(
            matterId, request.ArrangementType, request.RateCardId, request.FixedAmount, request.MilestonesJson,
            request.RetainerAmount, request.RetainerPeriod, request.AutoInvoiceDay, request.ReplenishmentThreshold, request.ContingencyPct), cancellationToken)));

    [HttpGet]
    [RequirePermission("rates.read.all")]
    public async Task<IActionResult> Get(Guid matterId, CancellationToken cancellationToken)
        => Ok(ApiResponse<BillingArrangementDto?>.Of(await mediator.Send(new GetBillingArrangementQuery(matterId), cancellationToken)));
}

public sealed record CreateRateCardRequest(string Name, Guid? BranchId, bool IsDefault);

public sealed record UpsertRateCardEntryRequest(string? Role, Guid? UserId, decimal Rate, string Currency, DateOnly EffectiveFrom);

public sealed record SetBillingArrangementRequest(
    string ArrangementType, Guid? RateCardId, decimal? FixedAmount, string? MilestonesJson,
    decimal? RetainerAmount, string? RetainerPeriod, int? AutoInvoiceDay, decimal? ReplenishmentThreshold, decimal? ContingencyPct);
