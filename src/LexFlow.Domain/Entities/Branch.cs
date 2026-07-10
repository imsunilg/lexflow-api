using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to core.branches (lexflow-database Scripts/02_Core/Branches).</summary>
public sealed class Branch : AuditableEntity
{
    private Branch()
    {
    }

    public Branch(Guid tenantId, string name, string code, string address = "{}", string tz = "Asia/Kolkata")
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Name = name;
        Code = code;
        Address = address;
        Tz = tz;
    }

    public string Name { get; private set; } = null!;
    public string Code { get; private set; } = null!;
    public string Address { get; private set; } = "{}";
    public string Tz { get; private set; } = "Asia/Kolkata";
    public string? Gstin { get; private set; }
    public string? SeriesPrefix { get; private set; }

    public void Update(string name, string code, string address, string tz, string? gstin, string? seriesPrefix)
    {
        Name = name;
        Code = code;
        Address = address;
        Tz = tz;
        Gstin = gstin;
        SeriesPrefix = seriesPrefix;
    }
}
