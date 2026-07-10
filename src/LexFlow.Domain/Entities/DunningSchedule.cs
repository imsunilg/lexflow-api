using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to fin.dunning_schedules (lexflow-database Scripts/06_Fin/DunningSchedules). Module 8: "reminder schedule (e.g., due−3, due, +7, +15, +30 days) per firm".</summary>
public sealed class DunningSchedule : AuditableEntity
{
    private DunningSchedule()
    {
    }

    public DunningSchedule(Guid tenantId, string name, string stepsJson)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Name = name;
        StepsJson = stepsJson;
        IsActive = true;
    }

    public string Name { get; private set; } = null!;
    public string StepsJson { get; private set; } = "[]";
    public bool IsActive { get; private set; }

    public void Update(string name, string stepsJson, bool isActive)
    {
        Name = name;
        StepsJson = stepsJson;
        IsActive = isActive;
    }
}
