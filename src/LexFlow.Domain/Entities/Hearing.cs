using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to legal.hearings (lexflow-database Scripts/04_Legal/Hearings). Module 5. G-AC2: date safety is non-negotiable here.</summary>
public sealed class Hearing : AuditableEntity
{
    private Hearing()
    {
    }

    public Hearing(Guid tenantId, Guid caseId, DateOnly date, TimeOnly? time, string courtTz, string? purpose, string? courtroom, Guid? assignedLawyerId)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        CaseId = caseId;
        Date = date;
        Time = time;
        CourtTz = courtTz;
        Purpose = purpose;
        Courtroom = courtroom;
        AssignedLawyerId = assignedLawyerId;
        Status = "Scheduled";
    }

    public Guid CaseId { get; private set; }
    public DateOnly Date { get; private set; }
    public TimeOnly? Time { get; private set; }
    public string CourtTz { get; private set; } = "Asia/Kolkata";
    public string? Purpose { get; private set; }
    public string? Courtroom { get; private set; }
    public Guid? AssignedLawyerId { get; private set; }
    public string Status { get; private set; } = "Scheduled";
    public bool PortalVisible { get; private set; }

    public void SetStatus(string status) => Status = status;

    /// <summary>Module 17/BR-10: nothing reaches the portal timeline without an explicit publish flag.</summary>
    public void SetPortalVisible(bool visible) => PortalVisible = visible;
}
