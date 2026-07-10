using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to fin.rate_cards (lexflow-database Scripts/06_Fin/RateCards). Module 8: "rate card: default firm rates by role, matter-level overrides per timekeeper".</summary>
public sealed class RateCard : AuditableEntity
{
    private RateCard()
    {
    }

    public RateCard(Guid tenantId, string name, Guid? branchId, bool isDefault)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Name = name;
        BranchId = branchId;
        IsDefault = isDefault;
        IsActive = true;
    }

    public string Name { get; private set; } = null!;
    public Guid? BranchId { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; }

    public void Update(string name, bool isDefault, bool isActive)
    {
        Name = name;
        IsDefault = isDefault;
        IsActive = isActive;
    }
}
