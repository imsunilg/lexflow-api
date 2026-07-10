namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 14 Teams CRUD (PRD §17: CRUD /api/v1/teams).</summary>
public interface ITeamService
{
    Task<IReadOnlyList<TeamDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<TeamDto?> GetByIdAsync(Guid tenantId, Guid teamId, CancellationToken cancellationToken = default);

    Task<TeamDto> CreateAsync(Guid tenantId, string name, Guid? leadUserId, IReadOnlyList<Guid> memberUserIds, CancellationToken cancellationToken = default);

    Task<TeamDto> UpdateAsync(Guid tenantId, Guid teamId, string name, Guid? leadUserId, IReadOnlyList<Guid> memberUserIds, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid tenantId, Guid teamId, CancellationToken cancellationToken = default);
}

public sealed record TeamDto(Guid Id, string Name, Guid? LeadUserId, IReadOnlyList<Guid> MemberUserIds);
