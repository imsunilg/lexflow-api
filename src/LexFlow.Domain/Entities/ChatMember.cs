namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to comm.chat_members (lexflow-database Scripts/08_Comm/ChatMembers).
/// Composite-PK join table (channel_id, user_id) — see MatterTeamMember for why this
/// does not derive from Entity/AuditableEntity.
/// </summary>
public sealed class ChatMember
{
    private ChatMember()
    {
    }

    public ChatMember(Guid tenantId, Guid channelId, Guid userId, string role)
    {
        TenantId = tenantId;
        ChannelId = channelId;
        UserId = userId;
        Role = role;
        JoinedAt = DateTimeOffset.UtcNow;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid TenantId { get; private set; }
    public Guid ChannelId { get; private set; }
    public Guid UserId { get; private set; }
    public string Role { get; private set; } = "member";
    public DateTimeOffset JoinedAt { get; private set; }
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
