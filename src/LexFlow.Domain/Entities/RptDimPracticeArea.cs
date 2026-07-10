namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to rpt.rpt_dim_practice_area (lexflow-database Scripts/17_Reporting_StarSchema/RptDimPracticeArea).
/// Module 13 star schema. Upserted by practice_area_key (= legal.practice_areas.id) by the hourly
/// incremental ETL job; never hard-deleted.
/// </summary>
public sealed class RptDimPracticeArea
{
    private RptDimPracticeArea()
    {
    }

    public RptDimPracticeArea(Guid practiceAreaKey, Guid tenantId, string? name, Guid? parentKey, bool isActive)
    {
        PracticeAreaKey = practiceAreaKey;
        TenantId = tenantId;
        Name = name;
        ParentKey = parentKey;
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public Guid PracticeAreaKey { get; private set; }
    public Guid TenantId { get; private set; }
    public string? Name { get; private set; }
    public Guid? ParentKey { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Upsert(string? name, Guid? parentKey, bool isActive)
    {
        Name = name;
        ParentKey = parentKey;
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
