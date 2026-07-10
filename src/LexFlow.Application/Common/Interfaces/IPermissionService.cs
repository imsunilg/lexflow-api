namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Resolves a user's effective permission set (primary role + additional grants,
/// PRD §21) for the PermissionHandler authorization pipeline. Implementations are
/// expected to cache in Redis with a short TTL (PRD §20(4): 60 s) and always fall
/// back to the database — "sensitive checks always DB-verified" (PRD Module 14 Edge Cases).
/// </summary>
public interface IPermissionService
{
    Task<IReadOnlyCollection<EffectivePermission>> GetEffectivePermissionsAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Invalidates the cached effective-permission set (role edit, grant change — PRD Module 14 Edge Cases: ≤ 60 s).</summary>
    Task InvalidateAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>GET /api/v1/permissions/catalog — the full, server-served, tenant-scoped permission catalog (PRD §21, Module 14 Error Handling: "catalog is server-served, UI renders dynamically").</summary>
    Task<IReadOnlyList<PermissionCatalogItem>> GetCatalogAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// AC-U2: "effective-permission inspector explains every allow with the granting
    /// rule chain" — for each effective permission, lists every role/grant that
    /// contributed it (a user with two roles granting the same permission gets two
    /// sources; PRD Module 14 Edge Cases: "union of grants").
    /// </summary>
    Task<IReadOnlyList<EffectivePermissionExplanation>> ExplainEffectivePermissionsAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

public sealed record EffectivePermission(string Key, string Module, string Action, string Scope);

public sealed record PermissionCatalogItem(Guid Id, string Key, string Module, string Action, string Scope, string? Label);

public sealed record GrantSource(string SourceType, Guid SourceId, string SourceName);

public sealed record EffectivePermissionExplanation(string Key, string Module, string Action, string Scope, IReadOnlyList<GrantSource> Sources);
