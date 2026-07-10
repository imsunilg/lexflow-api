using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to legal.hearing_outcomes (lexflow-database Scripts/04_Legal/HearingOutcomes). One per hearing (unique index).</summary>
public sealed class HearingOutcome : AuditableEntity
{
    private HearingOutcome()
    {
    }

    public HearingOutcome(Guid tenantId, Guid hearingId, string summary, string? adjournReason, Guid? recordedBy)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        HearingId = hearingId;
        Summary = summary;
        AdjournReason = adjournReason;
        RecordedBy = recordedBy;
        RecordedAt = DateTimeOffset.UtcNow;
    }

    public Guid HearingId { get; private set; }
    public string Summary { get; private set; } = null!;
    public string? AdjournReason { get; private set; }
    public Guid? RecordedBy { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public bool PortalVisible { get; private set; }

    /// <summary>Module 17/BR-10: nothing reaches the portal timeline without an explicit publish flag.</summary>
    public void SetPortalVisible(bool visible) => PortalVisible = visible;
}
