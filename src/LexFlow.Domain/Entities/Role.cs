using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to core.roles (lexflow-database Scripts/02_Core/Roles).</summary>
public sealed class Role : AuditableEntity
{
    private Role()
    {
    }

    public Role(Guid tenantId, string key, string name, bool isSystem = false)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Key = key;
        Name = name;
        IsSystem = isSystem;
    }

    public string Key { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public bool IsSystem { get; private set; }

    /// <summary>Custom (non-system) roles only — enforced by the handler, not the entity (PRD Module 14 Validation: system roles are immutable).</summary>
    public void Rename(string name) => Name = name;
}
