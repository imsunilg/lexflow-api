using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to legal.matter_important_dates (lexflow-database Scripts/04_Legal/MatterImportantDates). BR-2.</summary>
public sealed class MatterImportantDate : AuditableEntity
{
    private MatterImportantDate()
    {
    }

    public MatterImportantDate(Guid tenantId, Guid matterId, string kind, string title, DateTimeOffset dueAt, string reminderPolicyJson, string? severity)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        MatterId = matterId;
        Kind = kind;
        Title = title;
        DueAt = dueAt;
        ReminderPolicyJson = reminderPolicyJson;
        Severity = severity;
    }

    public Guid MatterId { get; private set; }
    public string Kind { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public DateTimeOffset DueAt { get; private set; }
    public DateTimeOffset? SatisfiedAt { get; private set; }
    public string? SatisfiedNote { get; private set; }
    public string ReminderPolicyJson { get; private set; } = "{}";
    public string? Severity { get; private set; }

    public void Update(string kind, string title, DateTimeOffset dueAt, string reminderPolicyJson, string? severity)
    {
        Kind = kind;
        Title = title;
        DueAt = dueAt;
        ReminderPolicyJson = reminderPolicyJson;
        Severity = severity;
    }

    public void MarkSatisfied(string note)
    {
        SatisfiedAt = DateTimeOffset.UtcNow;
        SatisfiedNote = note;
    }
}
