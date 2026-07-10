using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to legal.matter_parties (lexflow-database Scripts/04_Legal/MatterParties).</summary>
public sealed class MatterParty : AuditableEntity
{
    private MatterParty()
    {
    }

    public MatterParty(Guid tenantId, Guid matterId, string name, string partyRole, string? advocateName, string contactJson)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        MatterId = matterId;
        Name = name;
        PartyRole = partyRole;
        AdvocateName = advocateName;
        ContactJson = contactJson;
    }

    public Guid MatterId { get; private set; }
    public string Name { get; private set; } = null!;
    public string PartyRole { get; private set; } = null!;
    public string? AdvocateName { get; private set; }
    public string ContactJson { get; private set; } = "{}";

    public void Update(string name, string partyRole, string? advocateName, string contactJson)
    {
        Name = name;
        PartyRole = partyRole;
        AdvocateName = advocateName;
        ContactJson = contactJson;
    }
}
