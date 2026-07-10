using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to legal.matter_status_history (lexflow-database Scripts/04_Legal/MatterStatusHistory, additive migration).</summary>
public sealed class MatterStatusHistory : AuditableEntity
{
    private MatterStatusHistory()
    {
    }

    public MatterStatusHistory(Guid tenantId, Guid matterId, string? fromStatus, string toStatus, string? outcome, string? closureNote, Guid? byUser)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        MatterId = matterId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Outcome = outcome;
        ClosureNote = closureNote;
        At = DateTimeOffset.UtcNow;
        ByUser = byUser;
    }

    public Guid MatterId { get; private set; }
    public string? FromStatus { get; private set; }
    public string ToStatus { get; private set; } = null!;
    public string? Outcome { get; private set; }
    public string? ClosureNote { get; private set; }
    public DateTimeOffset At { get; private set; }
    public Guid? ByUser { get; private set; }
}
