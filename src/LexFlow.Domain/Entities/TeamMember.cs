namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to core.team_members (lexflow-database Scripts/02_Core/TeamMembers).
/// Composite-PK join table (team_id, user_id).
/// </summary>
public sealed class TeamMember
{
    private TeamMember()
    {
    }

    public TeamMember(Guid tenantId, Guid teamId, Guid userId)
    {
        TenantId = tenantId;
        TeamId = teamId;
        UserId = userId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid TenantId { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }
}
