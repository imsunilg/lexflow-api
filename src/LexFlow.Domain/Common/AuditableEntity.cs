namespace LexFlow.Domain.Common;

/// <summary>
/// Adds the tenancy, audit-stamp and soft-delete columns present on every business
/// table per PRD §14: tenant_id, created_at/created_by, updated_at/updated_by,
/// is_deleted/deleted_at/deleted_by. Column mappings live in the Infrastructure
/// layer's EF Core Fluent API configurations — this type carries no attributes
/// so Domain stays free of persistence concerns.
/// </summary>
public abstract class AuditableEntity : Entity
{
    public Guid TenantId { get; protected set; }
    public DateTimeOffset CreatedAt { get; protected set; }
    public Guid? CreatedBy { get; protected set; }
    public DateTimeOffset? UpdatedAt { get; protected set; }
    public Guid? UpdatedBy { get; protected set; }
    public bool IsDeleted { get; protected set; }
    public DateTimeOffset? DeletedAt { get; protected set; }
    public Guid? DeletedBy { get; protected set; }
}
