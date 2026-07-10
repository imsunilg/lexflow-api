using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to core.teams (lexflow-database Scripts/02_Core/Teams).</summary>
public sealed class Team : AuditableEntity
{
    private Team()
    {
    }

    public Team(Guid tenantId, string name, Guid? leadUserId = null)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Name = name;
        LeadUserId = leadUserId;
    }

    public string Name { get; private set; } = null!;
    public Guid? LeadUserId { get; private set; }

    public void Update(string name, Guid? leadUserId)
    {
        Name = name;
        LeadUserId = leadUserId;
    }
}
