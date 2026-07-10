using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Departments;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Departments;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>CRUD /api/v1/departments (PRD §17, Module 14).</summary>
[ApiController]
[Route("api/v1/departments")]
public sealed class DepartmentsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission("users.read.all")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<DepartmentDto>>.Of(await mediator.Send(new GetDepartmentsQuery(), cancellationToken)));

    [HttpGet("{id:guid}")]
    [RequirePermission("users.read.all")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<DepartmentDto>.Of(await mediator.Send(new GetDepartmentQuery(id), cancellationToken)));

    [HttpPost]
    [RequirePermission("users.manage.all")]
    public async Task<IActionResult> Create([FromBody] CreateDepartmentCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<DepartmentDto>.Of(await mediator.Send(command, cancellationToken)));

    [HttpPut("{id:guid}")]
    [RequirePermission("users.manage.all")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDepartmentRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<DepartmentDto>.Of(await mediator.Send(new UpdateDepartmentCommand(id, request.Name, request.Code, request.HeadUserId), cancellationToken)));

    [HttpDelete("{id:guid}")]
    [RequirePermission("users.manage.all")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteDepartmentCommand(id), cancellationToken);
        return NoContent();
    }
}

public sealed record UpdateDepartmentRequest(string Name, string? Code, Guid? HeadUserId);
