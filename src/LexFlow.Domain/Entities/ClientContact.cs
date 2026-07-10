using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to crm.client_contacts (lexflow-database Scripts/03_CRM/ClientContacts). Corporate contact persons.</summary>
public sealed class ClientContact : AuditableEntity
{
    private ClientContact()
    {
    }

    public ClientContact(Guid tenantId, Guid clientId, string name, string? designation, string? email, string? phone, bool isPrimary)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ClientId = clientId;
        Name = name;
        Designation = designation;
        Email = email;
        Phone = phone;
        IsPrimary = isPrimary;
    }

    public Guid ClientId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Designation { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public bool IsPrimary { get; private set; }

    public void Update(string name, string? designation, string? email, string? phone, bool isPrimary)
    {
        Name = name;
        Designation = designation;
        Email = email;
        Phone = phone;
        IsPrimary = isPrimary;
    }

    public void Reparent(Guid newClientId) => ClientId = newClientId;
}
