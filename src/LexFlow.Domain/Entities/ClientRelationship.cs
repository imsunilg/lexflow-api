using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to crm.client_relationships (lexflow-database
/// Scripts/03_CRM/ClientRelationships). Self-referencing relationship graph
/// (family/corporate-group/related-clients per Module 3).
/// </summary>
public sealed class ClientRelationship : AuditableEntity
{
    private ClientRelationship()
    {
    }

    public ClientRelationship(Guid tenantId, Guid clientId, Guid? relatedClientId, string? personName, string relationType)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ClientId = clientId;
        RelatedClientId = relatedClientId;
        PersonName = personName;
        RelationType = relationType;
    }

    public Guid ClientId { get; private set; }
    public Guid? RelatedClientId { get; private set; }
    public string? PersonName { get; private set; }
    public string RelationType { get; private set; } = null!;

    public void Reparent(Guid newClientId) => ClientId = newClientId;

    public void RepointRelatedClient(Guid newRelatedClientId) => RelatedClientId = newRelatedClientId;
}
