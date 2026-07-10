using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Users;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Users;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 14 user lifecycle (PRD §17).</summary>
[ApiController]
[Route("api/v1/users")]
public sealed class UsersController(IMediator mediator) : ControllerBase
{
    [HttpPost("invite")]
    [RequirePermission("users.manage.all")]
    public async Task<IActionResult> Invite([FromBody] InviteUserCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<UserDto>.Of(await mediator.Send(command, cancellationToken)));

    [HttpGet]
    [RequirePermission("users.read.all")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<UserDto>>.Of(await mediator.Send(new GetUsersQuery(), cancellationToken)));

    [HttpGet("{id:guid}")]
    [RequirePermission("users.read.all")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<UserDto>.Of(await mediator.Send(new GetUserQuery(id), cancellationToken)));

    [HttpPut("{id:guid}")]
    [RequirePermission("users.manage.all")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateUserCommand(
            id,
            request.Name,
            request.Designation,
            request.BarEnrollmentNo,
            request.Phone,
            request.CostRate,
            request.BranchId,
            request.DepartmentId,
            request.Tz,
            request.Locale,
            request.NotificationPrefsJson ?? "{}");

        return Ok(ApiResponse<UserDto>.Of(await mediator.Send(command, cancellationToken)));
    }

    [HttpPost("{id:guid}/suspend")]
    [RequirePermission("users.manage.all")]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new SuspendUserCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/reactivate")]
    [RequirePermission("users.manage.all")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new ReactivateUserCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>AC-U1/U3 deactivation wizard: takes the reassignment map (PRD Module 14).</summary>
    [HttpPost("{id:guid}/deactivate")]
    [RequirePermission("users.manage.all")]
    public async Task<IActionResult> Deactivate(Guid id, [FromBody] DeactivateUserRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeactivateUserCommand(id, request.Reassignments), cancellationToken);
        return NoContent();
    }

    /// <summary>Checklist of unresolved assignments a deactivation would need reassigned (drives the wizard's confirmation step).</summary>
    [HttpGet("{id:guid}/unresolved-assignments")]
    [RequirePermission("users.manage.all")]
    public async Task<IActionResult> GetUnresolvedAssignments(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<UnresolvedAssignment>>.Of(await mediator.Send(new GetUnresolvedAssignmentsQuery(id), cancellationToken)));

    /// <summary>AC-U2: effective-permission inspector ("why can Aditi see this matter?").</summary>
    [HttpGet("{id:guid}/effective-permissions")]
    [RequirePermission("users.read.all")]
    public async Task<IActionResult> GetEffectivePermissions(Guid id, [FromQuery] string? resource, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<EffectivePermissionExplanation>>.Of(await mediator.Send(new GetEffectivePermissionsQuery(id, resource), cancellationToken)));

    [HttpGet("{id:guid}/sessions")]
    [RequirePermission("users.manage.all")]
    public async Task<IActionResult> GetSessions(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<SessionDto>>.Of(await mediator.Send(new LexFlow.Application.Queries.Sessions.GetUserSessionsQuery(id), cancellationToken)));
}

public sealed record UpdateUserRequest(
    string Name,
    string? Designation,
    string? BarEnrollmentNo,
    string? Phone,
    decimal? CostRate,
    Guid? BranchId,
    Guid? DepartmentId,
    string? Tz,
    string? Locale,
    string? NotificationPrefsJson);

public sealed record DeactivateUserRequest(IReadOnlyList<ReassignmentEntry> Reassignments);
