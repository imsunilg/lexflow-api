using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to crm.client_addresses (lexflow-database Scripts/03_CRM/ClientAddresses). Typed addresses, one primary per type.</summary>
public sealed class ClientAddress : AuditableEntity
{
    private ClientAddress()
    {
    }

    public ClientAddress(
        Guid tenantId,
        Guid clientId,
        string kind,
        string line1,
        string? line2,
        string? city,
        string? stateCode,
        string? postal,
        string? country,
        bool isPrimaryOfKind)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ClientId = clientId;
        Kind = kind;
        Line1 = line1;
        Line2 = line2;
        City = city;
        StateCode = stateCode;
        Postal = postal;
        Country = country;
        IsPrimaryOfKind = isPrimaryOfKind;
    }

    public Guid ClientId { get; private set; }
    public string Kind { get; private set; } = null!;
    public string Line1 { get; private set; } = null!;
    public string? Line2 { get; private set; }
    public string? City { get; private set; }
    public string? StateCode { get; private set; }
    public string? Postal { get; private set; }
    public string? Country { get; private set; }
    public bool IsPrimaryOfKind { get; private set; }

    public void Update(string kind, string line1, string? line2, string? city, string? stateCode, string? postal, string? country, bool isPrimaryOfKind)
    {
        Kind = kind;
        Line1 = line1;
        Line2 = line2;
        City = city;
        StateCode = stateCode;
        Postal = postal;
        Country = country;
        IsPrimaryOfKind = isPrimaryOfKind;
    }

    public void SetPrimary(bool isPrimary) => IsPrimaryOfKind = isPrimary;

    public void Reparent(Guid newClientId) => ClientId = newClientId;
}
