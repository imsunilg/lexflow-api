namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to core.tenants (lexflow-database Scripts/02_Core/Tenants).
/// The one core table with no tenant_id column (it IS the tenant) and no
/// created_by/updated_by/deleted_by (no actor can exist before its own tenant does)
/// — so, unlike every other entity here, this does not derive from AuditableEntity.
/// </summary>
public sealed class Tenant : LexFlow.Domain.Common.Entity
{
    private Tenant()
    {
    }

    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public string Status { get; private set; } = "Active";
    public string PlanTier { get; private set; } = "Standard";
    public string Region { get; private set; } = "india-central";
    public string DefaultLocale { get; private set; } = "en-IN";
    public string DefaultCurrency { get; private set; } = "INR";
    public short FiscalYearStartMonth { get; private set; } = 4;
    public string Timezone { get; private set; } = "Asia/Kolkata";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsActive => !IsDeleted && Status == "Active";
}
