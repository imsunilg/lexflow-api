namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to rpt.rpt_dim_lawyer (lexflow-database Scripts/17_Reporting_StarSchema/RptDimLawyer).
/// Module 13 star schema. Upserted by lawyer_key (= core.users.id) by the hourly incremental ETL
/// job; never hard-deleted, even after the source user is deactivated — see the table's own
/// 001_Table.sql comment (edge case: "deleted lawyer in history (dim retains, marked inactive)").
/// </summary>
public sealed class RptDimLawyer
{
    private RptDimLawyer()
    {
    }

    public RptDimLawyer(Guid lawyerKey, Guid tenantId, string? name, string? roleKey, Guid? branchId, Guid? departmentId, bool isActive)
    {
        LawyerKey = lawyerKey;
        TenantId = tenantId;
        Name = name;
        RoleKey = roleKey;
        BranchId = branchId;
        DepartmentId = departmentId;
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public Guid LawyerKey { get; private set; }
    public Guid TenantId { get; private set; }
    public string? Name { get; private set; }
    public string? RoleKey { get; private set; }
    public Guid? BranchId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Upsert(string? name, string? roleKey, Guid? branchId, Guid? departmentId, bool isActive)
    {
        Name = name;
        RoleKey = roleKey;
        BranchId = branchId;
        DepartmentId = departmentId;
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
