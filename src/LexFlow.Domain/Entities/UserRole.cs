namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to core.user_roles (lexflow-database Scripts/02_Core/UserRoles).
/// Composite-PK join table (user_id, role_id) — see RolePermission for why this
/// does not derive from Entity/AuditableEntity.
/// </summary>
public sealed class UserRole
{
    private UserRole()
    {
    }

    public UserRole(Guid tenantId, Guid userId, Guid roleId)
    {
        TenantId = tenantId;
        UserId = userId;
        RoleId = roleId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }
}
