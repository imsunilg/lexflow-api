namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to rpt.rpt_dim_client (lexflow-database Scripts/17_Reporting_StarSchema/RptDimClient).
/// Module 13 star schema. Upserted by client_key (= crm.clients.id) by the hourly incremental ETL
/// job; never hard-deleted, even after the source client is deleted.
/// </summary>
public sealed class RptDimClient
{
    private RptDimClient()
    {
    }

    public RptDimClient(Guid clientKey, Guid tenantId, string? displayName, string? clientType, Guid? branchId, bool isActive)
    {
        ClientKey = clientKey;
        TenantId = tenantId;
        DisplayName = displayName;
        ClientType = clientType;
        BranchId = branchId;
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public Guid ClientKey { get; private set; }
    public Guid TenantId { get; private set; }
    public string? DisplayName { get; private set; }
    public string? ClientType { get; private set; }
    public Guid? BranchId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Upsert(string? displayName, string? clientType, Guid? branchId, bool isActive)
    {
        DisplayName = displayName;
        ClientType = clientType;
        BranchId = branchId;
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
