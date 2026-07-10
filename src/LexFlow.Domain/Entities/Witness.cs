using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to legal.witnesses (lexflow-database Scripts/04_Legal/Witnesses).</summary>
public sealed class Witness : AuditableEntity
{
    private Witness()
    {
    }

    public Witness(Guid tenantId, Guid caseId, string name, string? side, string contactJson, DateOnly? scheduledOn)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        CaseId = caseId;
        Name = name;
        Side = side;
        ContactJson = contactJson;
        ExamStatus = "ToBeExamined";
        ScheduledOn = scheduledOn;
    }

    public Guid CaseId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Side { get; private set; }
    public string ContactJson { get; private set; } = "{}";
    public string ExamStatus { get; private set; } = "ToBeExamined";
    public DateOnly? ScheduledOn { get; private set; }

    public void Update(string name, string? side, string contactJson, DateOnly? scheduledOn)
    {
        Name = name;
        Side = side;
        ContactJson = contactJson;
        ScheduledOn = scheduledOn;
    }

    public void SetExamStatus(string status) => ExamStatus = status;
}
