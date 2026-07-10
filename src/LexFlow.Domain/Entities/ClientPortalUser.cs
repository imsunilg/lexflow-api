using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to crm.client_portal_users (lexflow-database
/// Scripts/03_CRM/ClientPortalUsers). Portal identity, separate realm from staff
/// users (Module 17).
/// </summary>
public sealed class ClientPortalUser : AuditableEntity
{
    private ClientPortalUser()
    {
    }

    public ClientPortalUser(Guid tenantId, Guid clientId, string email, string? name)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ClientId = clientId;
        Email = email;
        Name = name;
        Status = "Invited";
        TwoFaEnabled = false;
    }

    public Guid ClientId { get; private set; }
    public string Email { get; private set; } = null!;
    public string? PasswordHash { get; private set; }
    public string? Name { get; private set; }
    public string Status { get; private set; } = "Invited";
    public bool TwoFaEnabled { get; private set; }
    public Guid[]? VisibleMatterIds { get; private set; }

    public void SetPasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
        Status = "Active";
    }

    public void Disable() => Status = "Deactivated";

    public void Reinvite() => Status = "Invited";

    public void Reparent(Guid newClientId) => ClientId = newClientId;

    public void SetVisibleMatterIds(Guid[]? matterIds) => VisibleMatterIds = matterIds;

    /// <summary>Module 17: ownership check backing every portal matter-scoped read/write. Null means "all matters visible" (default for an individual client's sole login); non-null restricts to a corporate client's per-user subset.</summary>
    public bool CanAccessMatter(Guid matterId) => VisibleMatterIds is null || VisibleMatterIds.Contains(matterId);
}
