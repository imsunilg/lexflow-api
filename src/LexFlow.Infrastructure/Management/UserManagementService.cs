using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Domain.Enums;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Management;

/// <summary>Module 14 user lifecycle (PRD §17, Module 14).</summary>
public sealed class UserManagementService(
    LexFlowDbContext db,
    IJwtTokenService jwtTokenService,
    IUserDenylistService denylistService) : IUserManagementService
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromHours(72);
    private const string InvitationAudience = "invitation";

    public async Task<UserDto> InviteAsync(Guid tenantId, Guid invitedBy, InviteUserInput input, CancellationToken cancellationToken = default)
    {
        var existing = await db.Users.SingleOrDefaultAsync(u => u.TenantId == tenantId && u.Email == input.Email, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException($"A user with email '{input.Email}' already exists for this tenant.", "USER_EMAIL_EXISTS");
        }

        var role = await db.Roles.SingleOrDefaultAsync(r => r.TenantId == tenantId && r.Id == input.RoleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), input.RoleId);

        var user = new User(tenantId, input.Email, input.Name, input.BranchId, input.DepartmentId);
        await db.Users.AddAsync(user, cancellationToken);
        await db.UserRoles.AddAsync(new UserRole(tenantId, user.Id, role.Id), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        // Invitation token issuance mirrors the password-reset purpose-token pattern
        // (IAuthService) — actual email delivery is a Communication-module concern,
        // out of scope here; a future accept-invite endpoint will consume this token.
        jwtTokenService.IssuePurposeToken(user.Id, tenantId, InvitationAudience, InvitationLifetime);

        return ToDto(user);
    }

    public async Task<UserDto?> GetByIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId, cancellationToken);
        return user is null ? null : ToDto(user);
    }

    public async Task<IReadOnlyList<UserDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var users = await db.Users.Where(u => u.TenantId == tenantId).OrderBy(u => u.Name).ToListAsync(cancellationToken);
        return users.Select(ToDto).ToList();
    }

    public async Task<UserDto> UpdateAsync(Guid tenantId, Guid userId, UpdateUserProfileInput input, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        user.UpdateProfile(
            input.Name,
            input.Designation,
            input.BarEnrollmentNo,
            input.Phone,
            input.CostRate,
            input.BranchId,
            input.DepartmentId,
            input.Tz,
            input.Locale,
            input.NotificationPrefsJson);

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(user);
    }

    public async Task SuspendAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetActiveOrThrowAsync(tenantId, userId, cancellationToken);
        user.Suspend();
        await db.SaveChangesAsync(cancellationToken);
        await denylistService.DenylistAsync(userId, cancellationToken);
    }

    public async Task ReactivateAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        user.Activate();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateAsync(Guid tenantId, Guid userId, IReadOnlyList<ReassignmentEntry> reassignments, CancellationToken cancellationToken = default)
    {
        var user = await GetActiveOrThrowAsync(tenantId, userId, cancellationToken);

        if (await IsLastOwnerAsync(tenantId, userId, cancellationToken))
        {
            throw new ForbiddenAccessException();
        }

        var unresolved = await GetUnresolvedAssignmentsAsync(tenantId, userId, cancellationToken);
        var reassignedTeamIds = reassignments
            .Where(r => r.EntityType == "team_lead")
            .Select(r => r.EntityId)
            .ToHashSet();

        var stillUnresolved = unresolved.Where(u => !reassignedTeamIds.Contains(u.EntityId)).ToList();
        if (stillUnresolved.Count > 0)
        {
            throw new ConflictException(
                "Cannot deactivate: unresolved assignments must be reassigned first.",
                "UNRESOLVED_ASSIGNMENTS");
        }

        foreach (var reassignment in reassignments.Where(r => r.EntityType == "team_lead"))
        {
            var team = await db.Teams.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Id == reassignment.EntityId, cancellationToken);
            team?.Update(team.Name, reassignment.NewAssigneeUserId);
        }

        var activeSessions = await db.UserSessions
            .Where(s => s.TenantId == tenantId && s.UserId == userId && s.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in activeSessions)
        {
            session.Revoke();
        }

        user.Deactivate();
        await db.SaveChangesAsync(cancellationToken);

        // AC-U1: denylist takes effect immediately — checked on every request via
        // JwtBearerEvents.OnTokenValidated, well under the 60 s budget.
        await denylistService.DenylistAsync(userId, cancellationToken);
    }

    public async Task<IReadOnlyList<UnresolvedAssignment>> GetUnresolvedAssignmentsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var ledTeams = await db.Teams
            .Where(t => t.TenantId == tenantId && t.LeadUserId == userId)
            .ToListAsync(cancellationToken);

        return ledTeams
            .Select(t => new UnresolvedAssignment("team_lead", t.Id, $"Leads team '{t.Name}' — reassign lead before deactivating."))
            .ToList();
    }

    private async Task<bool> IsLastOwnerAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var isOwner = await (
            from userRole in db.UserRoles
            join role in db.Roles on userRole.RoleId equals role.Id
            where userRole.TenantId == tenantId && userRole.UserId == userId && role.Key == "owner"
            select userRole).AnyAsync(cancellationToken);

        if (!isOwner)
        {
            return false;
        }

        var ownerCount = await (
            from userRole in db.UserRoles
            join role in db.Roles on userRole.RoleId equals role.Id
            where userRole.TenantId == tenantId && role.Key == "owner"
            select userRole.UserId).Distinct().CountAsync(cancellationToken);

        return ownerCount <= 1;
    }

    private async Task<User> GetActiveOrThrowAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        return await db.Users.SingleOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);
    }

    private static UserDto ToDto(User user) => new(
        user.Id,
        user.Email,
        user.Name,
        user.Designation,
        user.BarEnrollmentNo,
        user.Phone,
        user.CostRate,
        user.BranchId,
        user.DepartmentId,
        user.Status.ToString(),
        user.Tz,
        user.Locale,
        user.TwoFaEnabled);
}
