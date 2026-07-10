namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 14 (User Management) user lifecycle operations — PRD §17 (invite/get/put/suspend/reactivate/deactivate).</summary>
public interface IUserManagementService
{
    Task<UserDto> InviteAsync(Guid tenantId, Guid invitedBy, InviteUserInput input, CancellationToken cancellationToken = default);

    Task<UserDto?> GetByIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<UserDto> UpdateAsync(Guid tenantId, Guid userId, UpdateUserProfileInput input, CancellationToken cancellationToken = default);

    Task SuspendAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task ReactivateAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// AC-U1 (deactivated user's active JWTs rejected ≤ 60 s) + AC-U3 (last Owner cannot
    /// be demoted/deactivated by any path). Reassignments cover what this build has a
    /// domain model for today (team leadership); Matter/Task/Lead reassignment wires in
    /// once those modules exist — the 409 checklist shape below already anticipates them.
    /// </summary>
    Task DeactivateAsync(Guid tenantId, Guid userId, IReadOnlyList<ReassignmentEntry> reassignments, CancellationToken cancellationToken = default);

    /// <summary>Returns the checklist of unresolved assignments a deactivation would need reassigned, without performing it.</summary>
    Task<IReadOnlyList<UnresolvedAssignment>> GetUnresolvedAssignmentsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
}

public sealed record InviteUserInput(string Email, string Name, Guid RoleId, Guid? BranchId, Guid? DepartmentId);

public sealed record UpdateUserProfileInput(
    string Name,
    string? Designation,
    string? BarEnrollmentNo,
    string? Phone,
    decimal? CostRate,
    Guid? BranchId,
    Guid? DepartmentId,
    string? Tz,
    string? Locale,
    string NotificationPrefsJson);

public sealed record ReassignmentEntry(string EntityType, Guid EntityId, Guid NewAssigneeUserId);

public sealed record UnresolvedAssignment(string EntityType, Guid EntityId, string Description);

public sealed record UserDto(
    Guid Id,
    string Email,
    string Name,
    string? Designation,
    string? BarEnrollmentNo,
    string? Phone,
    decimal? CostRate,
    Guid? BranchId,
    Guid? DepartmentId,
    string Status,
    string? Tz,
    string? Locale,
    bool TwoFaEnabled);
