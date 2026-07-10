using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to crm.lead_activities (lexflow-database
/// Scripts/03_CRM/LeadActivities). Calls/emails/meetings/notes unified with
/// <see cref="ActivityType"/> (Module 2).
/// </summary>
public sealed class LeadActivity : AuditableEntity
{
    private LeadActivity()
    {
    }

    public LeadActivity(
        Guid tenantId,
        Guid leadId,
        string activityType,
        string? direction,
        int? durationMin,
        string? subject,
        string? body,
        string? outcome,
        Guid? loggedBy)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        LeadId = leadId;
        ActivityType = activityType;
        Direction = direction;
        DurationMin = durationMin;
        Subject = subject;
        Body = body;
        Outcome = outcome;
        OccurredAt = DateTimeOffset.UtcNow;
        LoggedBy = loggedBy;
    }

    public Guid LeadId { get; private set; }
    public string ActivityType { get; private set; } = null!;
    public string? Direction { get; private set; }
    public int? DurationMin { get; private set; }
    public string? Subject { get; private set; }
    public string? Body { get; private set; }
    public string? Outcome { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public Guid? LoggedBy { get; private set; }
}
