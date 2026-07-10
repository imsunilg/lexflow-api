namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to core.user_permission_grants
/// (lexflow-database Scripts/02_Core/UserPermissionGrants). Composite-PK join table
/// (user_id, permission_id) for ad-hoc grants beyond a user's primary role.
/// </summary>
public sealed class UserPermissionGrant
{
    private UserPermissionGrant()
    {
    }

    public UserPermissionGrant(Guid tenantId, Guid userId, Guid permissionId, Guid? grantedBy = null)
    {
        TenantId = tenantId;
        UserId = userId;
        PermissionId = permissionId;
        GrantedBy = grantedBy;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid PermissionId { get; private set; }
    public Guid? GrantedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }
}
