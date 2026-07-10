namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to core.tenant_settings (lexflow-database
/// Scripts/02_Core/TenantSettings). Composite-PK (tenant_id, key) — one row per
/// Settings section (PRD Module 15) per tenant; PUT /api/v1/settings/{section} upserts.
/// </summary>
public sealed class TenantSetting
{
    private TenantSetting()
    {
    }

    public TenantSetting(Guid tenantId, string key, string valueJson)
    {
        TenantId = tenantId;
        Key = key;
        ValueJson = valueJson;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid TenantId { get; private set; }
    public string Key { get; private set; } = null!;
    public string ValueJson { get; private set; } = "{}";
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }

    public void SetValue(string valueJson) => ValueJson = valueJson;
}
