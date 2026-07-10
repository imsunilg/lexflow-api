using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to fin.activity_codes (lexflow-database Scripts/06_Fin/ActivityCodes). Module 9: configurable time-entry activity types.</summary>
public sealed class ActivityCode : AuditableEntity
{
    private ActivityCode()
    {
    }

    public ActivityCode(Guid tenantId, string name, bool isBillableDefault)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Name = name;
        IsBillableDefault = isBillableDefault;
        IsActive = true;
    }

    public string Name { get; private set; } = null!;
    public bool IsBillableDefault { get; private set; }
    public bool IsActive { get; private set; }

    public void Update(string name, bool isBillableDefault, bool isActive)
    {
        Name = name;
        IsBillableDefault = isBillableDefault;
        IsActive = isActive;
    }
}
