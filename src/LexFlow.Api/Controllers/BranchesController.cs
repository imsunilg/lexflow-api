using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Branches;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Branches;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>CRUD /api/v1/branches (PRD §17, Module 14).</summary>
[ApiController]
[Route("api/v1/branches")]
public sealed class BranchesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission("users.read.all")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<BranchDto>>.Of(await mediator.Send(new GetBranchesQuery(), cancellationToken)));

    [HttpGet("{id:guid}")]
    [RequirePermission("users.read.all")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<BranchDto>.Of(await mediator.Send(new GetBranchQuery(id), cancellationToken)));

    [HttpPost]
    [RequirePermission("users.manage.all")]
    public async Task<IActionResult> Create([FromBody] CreateBranchCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<BranchDto>.Of(await mediator.Send(command, cancellationToken)));

    [HttpPut("{id:guid}")]
    [RequirePermission("users.manage.all")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBranchRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<BranchDto>.Of(await mediator.Send(
            new UpdateBranchCommand(id, request.Name, request.Code, request.AddressJson, request.Tz, request.Gstin, request.SeriesPrefix),
            cancellationToken)));

    [HttpDelete("{id:guid}")]
    [RequirePermission("users.manage.all")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteBranchCommand(id), cancellationToken);
        return NoContent();
    }
}

public sealed record UpdateBranchRequest(string Name, string Code, string AddressJson, string Tz, string? Gstin, string? SeriesPrefix);
