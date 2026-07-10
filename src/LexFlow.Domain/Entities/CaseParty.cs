using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to legal.case_parties (lexflow-database Scripts/04_Legal/CaseParties).</summary>
public sealed class CaseParty : AuditableEntity
{
    private CaseParty()
    {
    }

    public CaseParty(Guid tenantId, Guid caseId, string partyRole, string name, string? advocateName, Guid? advocateUserId, string contactJson)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        CaseId = caseId;
        PartyRole = partyRole;
        Name = name;
        AdvocateName = advocateName;
        AdvocateUserId = advocateUserId;
        ContactJson = contactJson;
    }

    public Guid CaseId { get; private set; }
    public string PartyRole { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? AdvocateName { get; private set; }
    public Guid? AdvocateUserId { get; private set; }
    public string ContactJson { get; private set; } = "{}";

    public void Update(string partyRole, string name, string? advocateName, Guid? advocateUserId, string contactJson)
    {
        PartyRole = partyRole;
        Name = name;
        AdvocateName = advocateName;
        AdvocateUserId = advocateUserId;
        ContactJson = contactJson;
    }
}
