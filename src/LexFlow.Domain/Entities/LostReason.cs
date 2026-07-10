using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to crm.lost_reasons (lexflow-database Scripts/03_CRM/LostReasons).</summary>
public sealed class LostReason : AuditableEntity
{
    private LostReason()
    {
    }

    public LostReason(Guid tenantId, string name)
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
