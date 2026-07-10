using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to comm.chat_channels (lexflow-database Scripts/08_Comm/ChatChannels). Kind: Firm|Team|Matter|DM.</summary>
public sealed class ChatChannel : AuditableEntity
{
    private ChatChannel()
    {
    }

    public ChatChannel(Guid tenantId, string kind, string? name, Guid? matterId, Guid? teamId, int? retentionDays)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Kind = kind;
        Name = name;
        MatterId = matterId;
        TeamId = teamId;
        RetentionDays = retentionDays;
    }

    public string Kind { get; private set; } = null!;
    public string? Name { get; private set; }
    public Guid? MatterId { get; private set; }
    public Guid? TeamId { get; private set; }
    public int? RetentionDays { get; private set; }
}
