using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to core.permissions (lexflow-database Scripts/02_Core/Permissions).
/// Key is the "module.action.scope" string described in PRD §20/§21 (e.g. "matters.read.own").
/// </summary>
public sealed class Permission : AuditableEntity
{
    private Permission()
    {
    }

    public Permission(Guid tenantId, string key, string module, string action, string scope, string? label = null)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Key = key;
        Module = module;
        Action = action;
        Scope = scope;
        Label = label;
    }

    public string Key { get; private set; } = null!;
    public string Module { get; private set; } = null!;
    public string Action { get; private set; } = null!;
    public string Scope { get; private set; } = null!;
    public string? Label { get; private set; }
}
