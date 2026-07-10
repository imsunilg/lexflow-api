namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 13 Custom Report Builder: "Pick base entity (Matter/Invoice/TimeEntry/Lead/Task/
/// Hearing) -&gt; choose columns (whitelisted field catalog with joins pre-modeled) -&gt; filters
/// (AND/OR groups) -&gt; group-by + aggregates (sum/avg/count/min/max) -&gt; sort -&gt; preview -&gt; save
/// (private/team/firm) -&gt; schedule." Validation: "custom builder field whitelist only (no raw
/// SQL ever)". Every field/filter/group-by/sort key in a definition is checked against
/// <c>ReportFieldCatalog</c> before any query is built.
/// </summary>
public interface ICustomReportService
{
    Task<ReportDefinitionDto> CreateAsync(Guid tenantId, Guid ownerId, CustomReportDefinitionInput input, CancellationToken cancellationToken = default);

    Task<ReportDefinitionDto> UpdateAsync(Guid tenantId, Guid definitionId, CustomReportDefinitionInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportDefinitionDto>> GetForOwnerAsync(Guid tenantId, Guid ownerId, CancellationToken cancellationToken = default);

    Task<ReportDefinitionDto?> GetAsync(Guid tenantId, Guid definitionId, CancellationToken cancellationToken = default);

    Task<ReportResult> RunAsync(Guid tenantId, Guid definitionId, ReportScope scope, CancellationToken cancellationToken = default);
}

/// <summary>One AND/OR group of leaf comparisons; <see cref="Groups"/> nests further groups (an empty list = leaf-only group).</summary>
public sealed record CustomReportFilterGroup(string Logic, IReadOnlyList<CustomReportFilter> Filters, IReadOnlyList<CustomReportFilterGroup>? Groups = null);

public sealed record CustomReportFilter(string Field, string Operator, string? Value);

public sealed record CustomReportAggregate(string Field, string Function, string? Alias);

public sealed record CustomReportSort(string Field, bool Descending);

public sealed record CustomReportDefinitionInput(
    string Name,
    string BaseEntity,
    IReadOnlyList<string> Columns,
    CustomReportFilterGroup? Filter,
    IReadOnlyList<string> GroupBy,
    IReadOnlyList<CustomReportAggregate> Aggregates,
    IReadOnlyList<CustomReportSort> Sort,
    string Visibility);

public sealed record ReportDefinitionDto(Guid Id, string Name, string BaseEntity, CustomReportDefinitionInput Definition, string Visibility, Guid OwnerId, bool IsActive);
