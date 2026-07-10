using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Roles;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Roles;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 14 Roles CRUD (PRD §17, §21).</summary>
[ApiController]
[Route("api/v1/roles")]
public sealed class RolesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission("users.read.all")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<RoleDto>>.Of(await mediator.Send(new GetRolesQuery(), cancellationToken)));

    [HttpGet("{id:guid}")]
    [RequirePermission("users.read.all")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<RoleDto>.Of(await mediator.Send(new GetRoleQuery(id), cancellationToken)));

    [HttpPost]
    [RequirePermission("users.manage.all")]
    public async Task<IActionResult> Create([FromBody] CreateRoleCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<RoleDto>.Of(await mediator.Send(command, cancellationToken)));

    [HttpPut("{id:guid}")]
    [RequirePermission("users.manage.all")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoleRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<RoleDto>.Of(await mediator.Send(new UpdateRoleCommand(id, request.Name, request.PermissionIds), cancellationToken)));
}

public sealed record UpdateRoleRequest(string Name, IReadOnlyList<Guid> PermissionIds);
