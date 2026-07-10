namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to core.role_permissions (lexflow-database Scripts/02_Core/RolePermissions).
/// Composite-PK join table (role_id, permission_id) — no surrogate id, so this does
/// not derive from Entity/AuditableEntity (neither of which fits a table with no id column).
/// </summary>
public sealed class RolePermission
{
    private RolePermission()
    {
    }

    public RolePermission(Guid tenantId, Guid roleId, Guid permissionId)
    {
        TenantId = tenantId;
        RoleId = roleId;
        PermissionId = permissionId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid TenantId { get; private set; }
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }
}
