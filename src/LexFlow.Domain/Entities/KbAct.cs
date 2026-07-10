using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to kb.kb_acts (lexflow-database Scripts/09_KB/KbActs). Module 12: "Acts (hierarchy: Act → Chapter → Section → sub-section text)".</summary>
public sealed class KbAct : AuditableEntity
{
    private KbAct()
    {
    }

    public KbAct(Guid tenantId, string name, string? shortCode, string? jurisdiction, int? year)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Name = name;
        ShortCode = shortCode;
        Jurisdiction = jurisdiction;
        Year = year;
    }

    public string Name { get; private set; } = null!;
    public string? ShortCode { get; private set; }
    public string? Jurisdiction { get; private set; }
    public int? Year { get; private set; }

    public void Update(string name, string? shortCode, string? jurisdiction, int? year)
    {
        Name = name;
        ShortCode = shortCode;
        Jurisdiction = jurisdiction;
        Year = year;
    }
}
