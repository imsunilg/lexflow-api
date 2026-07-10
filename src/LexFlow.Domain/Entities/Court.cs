using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to legal.courts (lexflow-database Scripts/04_Legal/Courts). Master list, seeded per tenant.</summary>
public sealed class Court : AuditableEntity
{
    private Court()
    {
    }

    public Court(Guid tenantId, string name, string level, string? city, string? state, string? bench, string tz = "Asia/Kolkata")
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Name = name;
        Level = level;
        City = city;
        State = state;
        Bench = bench;
        Tz = tz;
    }

    public string Name { get; private set; } = null!;
    public string Level { get; private set; } = null!;
    public string? City { get; private set; }
    public string? State { get; private set; }
    public string? Bench { get; private set; }
    public string Tz { get; private set; } = "Asia/Kolkata";
}
