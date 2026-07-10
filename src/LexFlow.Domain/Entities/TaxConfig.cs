using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to fin.tax_configs (lexflow-database Scripts/06_Fin/TaxConfigs). Backs Settings §8 Taxes.</summary>
public sealed class TaxConfig : AuditableEntity
{
    private TaxConfig()
    {
    }

    public TaxConfig(Guid tenantId, string countryCode, string taxType, string componentsJson, Guid? branchId = null)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        BranchId = branchId;
        CountryCode = countryCode;
        TaxType = taxType;
        ComponentsJson = componentsJson;
        IsActive = true;
    }

    public Guid? BranchId { get; private set; }
    public string CountryCode { get; private set; } = null!;
    public string TaxType { get; private set; } = null!;
    public string ComponentsJson { get; private set; } = "{}";
    public bool IsActive { get; private set; }

    public void Update(string countryCode, string taxType, string componentsJson, bool isActive)
    {
        CountryCode = countryCode;
        TaxType = taxType;
        ComponentsJson = componentsJson;
        IsActive = isActive;
    }
}
