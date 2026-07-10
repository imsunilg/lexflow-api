using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Dunning;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Dunning;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 8 dunning: reminder schedule + per-invoice mute. The actual send is driven by IDunningService.RunDueRemindersAsync, a Hangfire recurring job (see LexFlow.Workers/Program.cs).</summary>
[ApiController]
[Route("api/v1/dunning")]
public sealed class DunningController(IMediator mediator) : ControllerBase
{
    [HttpPost("schedules")]
    [RequirePermission("invoices.update.all")]
    public async Task<IActionResult> UpsertSchedule([FromBody] UpsertDunningScheduleRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<DunningScheduleDto>.Of(await mediator.Send(new UpsertDunningScheduleCommand(request.Name, request.StepsJson, request.IsActive), cancellationToken)));

    [HttpGet("schedules")]
    [RequirePermission("invoices.read.all")]
    public async Task<IActionResult> GetSchedules(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<DunningScheduleDto>>.Of(await mediator.Send(new GetDunningSchedulesQuery(), cancellationToken)));

    [HttpPost("invoices/{invoiceId:guid}/mute")]
    [RequirePermission("invoices.update.own")]
    public async Task<IActionResult> Mute(Guid invoiceId, CancellationToken cancellationToken)
    {
        await mediator.Send(new MuteDunningCommand(invoiceId), cancellationToken);
        return NoContent();
    }
}

public sealed record UpsertDunningScheduleRequest(string Name, string StepsJson, bool IsActive);
