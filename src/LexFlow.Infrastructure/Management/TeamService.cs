using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Management;

/// <summary>Module 14 Teams CRUD (PRD §17).</summary>
public sealed class TeamService(LexFlowDbContext db) : ITeamService
{
    public async Task<IReadOnlyList<TeamDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var teams = await db.Teams.Where(t => t.TenantId == tenantId).OrderBy(t => t.Name).ToListAsync(cancellationToken);
        var result = new List<TeamDto>(teams.Count);
        foreach (var team in teams)
        {
            result.Add(await ToDtoAsync(team, cancellationToken));
        }

        return result;
    }

    public async Task<TeamDto?> GetByIdAsync(Guid tenantId, Guid teamId, CancellationToken cancellationToken = default)
    {
        var team = await db.Teams.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Id == teamId, cancellationToken);
        return team is null ? null : await ToDtoAsync(team, cancellationToken);
    }

    public async Task<TeamDto> CreateAsync(Guid tenantId, string name, Guid? leadUserId, IReadOnlyList<Guid> memberUserIds, CancellationToken cancellationToken = default)
    {
        var team = new Team(tenantId, name, leadUserId);
        await db.Teams.AddAsync(team, cancellationToken);
        await SetMembersAsync(tenantId, team.Id, memberUserIds, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(team, cancellationToken);
    }

    public async Task<TeamDto> UpdateAsync(Guid tenantId, Guid teamId, string name, Guid? leadUserId, IReadOnlyList<Guid> memberUserIds, CancellationToken cancellationToken = default)
    {
        var team = await db.Teams.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Id == teamId, cancellationToken)
            ?? throw new NotFoundException(nameof(Team), teamId);

        team.Update(name, leadUserId);

        var existingMembers = await db.TeamMembers.Where(m => m.TenantId == tenantId && m.TeamId == teamId).ToListAsync(cancellationToken);
        db.TeamMembers.RemoveRange(existingMembers);
        await SetMembersAsync(tenantId, teamId, memberUserIds, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(team, cancellationToken);
    }

    public async Task DeleteAsync(Guid tenantId, Guid teamId, CancellationToken cancellationToken = default)
    {
        var team = await db.Teams.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Id == teamId, cancellationToken)
            ?? throw new NotFoundException(nameof(Team), teamId);

        var members = await db.TeamMembers.Where(m => m.TenantId == tenantId && m.TeamId == teamId).ToListAsync(cancellationToken);
        db.TeamMembers.RemoveRange(members);
        db.Teams.Remove(team);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SetMembersAsync(Guid tenantId, Guid teamId, IReadOnlyList<Guid> memberUserIds, CancellationToken cancellationToken)
    {
        foreach (var userId in memberUserIds.Distinct())
        {
            await db.TeamMembers.AddAsync(new TeamMember(tenantId, teamId, userId), cancellationToken);
        }
    }

    private async Task<TeamDto> ToDtoAsync(Team team, CancellationToken cancellationToken)
    {
        var memberIds = await db.TeamMembers
            .Where(m => m.TenantId == team.TenantId && m.TeamId == team.Id)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);

        return new TeamDto(team.Id, team.Name, team.LeadUserId, memberIds);
    }
}
