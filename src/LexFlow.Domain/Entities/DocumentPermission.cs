using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to dms.document_permissions (lexflow-database Scripts/05_DMS/DocumentPermissions). principal_id is polymorphic per principal_type (user/team/role/portal_client/link).</summary>
public sealed class DocumentPermission : AuditableEntity
{
    private DocumentPermission()
    {
    }

    public DocumentPermission(Guid tenantId, Guid documentId, string principalType, Guid principalId, string access)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        DocumentId = documentId;
        PrincipalType = principalType;
        PrincipalId = principalId;
        Access = access;
    }

    public Guid DocumentId { get; private set; }
    public string PrincipalType { get; private set; } = null!;
    public Guid PrincipalId { get; private set; }
    public string Access { get; private set; } = null!;

    public void SetAccess(string access) => Access = access;
}
