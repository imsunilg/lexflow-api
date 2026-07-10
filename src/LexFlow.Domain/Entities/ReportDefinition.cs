using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to rpt.report_definitions (lexflow-database Scripts/17_Reporting_StarSchema/ReportDefinitions).
/// Module 13 Custom Report Builder. <see cref="DefinitionJson"/> holds the whole builder spec
/// (columns/filters/groupBy/aggregates/sort) — validated against the whitelisted field catalog
/// on every read/run by <c>ReportFieldCatalog</c>, never executed as raw SQL.
/// </summary>
public sealed class ReportDefinition : AuditableEntity
{
    private ReportDefinition()
    {
    }

    public ReportDefinition(Guid tenantId, string name, string baseEntity, string definitionJson, string visibility, Guid ownerId)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Name = name;
        BaseEntity = baseEntity;
        DefinitionJson = definitionJson;
        Visibility = visibility;
        OwnerId = ownerId;
        IsActive = true;
    }

    public string Name { get; private set; } = null!;
    public string BaseEntity { get; private set; } = null!;
    public string DefinitionJson { get; private set; } = "{}";
    public string Visibility { get; private set; } = "private";
    public Guid OwnerId { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void Update(string name, string definitionJson, string visibility)
    {
        Name = name;
        DefinitionJson = definitionJson;
        Visibility = visibility;
    }

    public void Deactivate() => IsActive = false;
}
