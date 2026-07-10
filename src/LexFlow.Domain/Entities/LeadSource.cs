using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to crm.lead_sources (lexflow-database Scripts/03_CRM/LeadSources).</summary>
public sealed class LeadSource : AuditableEntity
{
    private LeadSource()
    {
    }

    public LeadSource(Guid tenantId, string name)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Name = name;
        IsActive = true;
    }

    public string Name { get; private set; } = null!;
    public bool IsActive { get; private set; }

    public void Rename(string name) => Name = name;

    public void SetActive(bool isActive) => IsActive = isActive;
}
