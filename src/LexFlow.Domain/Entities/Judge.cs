using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to legal.judges (lexflow-database Scripts/04_Legal/Judges).</summary>
public sealed class Judge : AuditableEntity
{
    private Judge()
    {
    }

    public Judge(Guid tenantId, string name, Guid courtId)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Name = name;
        CourtId = courtId;
        Active = true;
    }

    public string Name { get; private set; } = null!;
    public Guid CourtId { get; private set; }
    public bool Active { get; private set; }

    public void SetActive(bool active) => Active = active;
}
