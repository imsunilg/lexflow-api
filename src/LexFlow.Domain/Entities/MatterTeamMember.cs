namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to legal.matter_team_members (lexflow-database
/// Scripts/04_Legal/MatterTeamMembers). Composite-PK join table (matter_id, user_id) —
/// see UserRole for why this does not derive from Entity/AuditableEntity.
/// </summary>
public sealed class MatterTeamMember
{
    private MatterTeamMember()
    {
    }

    public MatterTeamMember(Guid tenantId, Guid matterId, Guid userId, string? roleInMatter, decimal? rateOverride)
    {
        TenantId = tenantId;
        MatterId = matterId;
        UserId = userId;
        RoleInMatter = roleInMatter;
        RateOverride = rateOverride;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid TenantId { get; private set; }
    public Guid MatterId { get; private set; }
    public Guid UserId { get; private set; }
    public string? RoleInMatter { get; private set; }
    public decimal? RateOverride { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
    }
}
