using System.Text.Json;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Reporting;

/// <summary>
/// Module 13 Custom Report Builder. Every definition is validated against <see cref="ReportFieldCatalog"/>
/// before it is ever saved or run — an unknown column/filter/group-by/sort field is rejected with
/// DomainRuleException("FIELD_NOT_WHITELISTED"), never passed to a query (Validation: "custom
/// builder field whitelist only (no raw SQL ever)"). Rows are loaded per whitelisted base entity,
/// tenant- and scope-filtered at the source query, then materialized into plain
/// Dictionary&lt;string, object?&gt; rows so the filter/group-by/aggregate/sort pipeline that follows
/// operates purely over the whitelisted field set — there is no code path from a definition to
/// dynamically-built SQL.
/// </summary>
public sealed class CustomReportService(LexFlowDbContext db) : ICustomReportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ReportDefinitionDto> CreateAsync(Guid tenantId, Guid ownerId, CustomReportDefinitionInput input, CancellationToken cancellationToken = default)
    {
        ValidateDefinition(input);

        var entity = new ReportDefinition(tenantId, input.Name, input.BaseEntity, JsonSerializer.Serialize(input, JsonOptions), input.Visibility, ownerId);
        await db.ReportDefinitions.AddAsync(entity, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity, input);
    }

    public async Task<ReportDefinitionDto> UpdateAsync(Guid tenantId, Guid definitionId, CustomReportDefinitionInput input, CancellationToken cancellationToken = default)
    {
        ValidateDefinition(input);

        var entity = await db.ReportDefinitions.SingleOrDefaultAsync(d => d.TenantId == tenantId && d.Id == definitionId, cancellationToken)
            ?? throw new NotFoundException(nameof(ReportDefinition), definitionId);

        entity.Update(input.Name, JsonSerializer.Serialize(input, JsonOptions), input.Visibility);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity, input);
    }

    public async Task<IReadOnlyList<ReportDefinitionDto>> GetForOwnerAsync(Guid tenantId, Guid ownerId, CancellationToken cancellationToken = default)
    {
        var entities = await db.ReportDefinitions
            .Where(d => d.TenantId == tenantId && d.IsActive && (d.OwnerId == ownerId || d.Visibility == "firm" || d.Visibility == "team"))
            .ToListAsync(cancellationToken);

        return entities.Select(e => ToDto(e, Deserialize(e))).ToList();
    }

    public async Task<ReportDefinitionDto?> GetAsync(Guid tenantId, Guid definitionId, CancellationToken cancellationToken = default)
    {
        var entity = await db.ReportDefinitions.SingleOrDefaultAsync(d => d.TenantId == tenantId && d.Id == definitionId, cancellationToken);
        return entity is null ? null : ToDto(entity, Deserialize(entity));
    }

    public async Task<ReportResult> RunAsync(Guid tenantId, Guid definitionId, ReportScope scope, CancellationToken cancellationToken = default)
    {
        var entity = await db.ReportDefinitions.SingleOrDefaultAsync(d => d.TenantId == tenantId && d.Id == definitionId, cancellationToken)
            ?? throw new NotFoundException(nameof(ReportDefinition), definitionId);

        var input = Deserialize(entity);
        var rows = await LoadRowsAsync(tenantId, input.BaseEntity, scope, cancellationToken);
        return Build(rows, input);
    }

    private static void ValidateDefinition(CustomReportDefinitionInput input)
    {
        if (!ReportFieldCatalog.IsValidBaseEntity(input.BaseEntity))
        {
            throw new DomainRuleException("BASE_ENTITY_NOT_WHITELISTED", $"'{input.BaseEntity}' is not a whitelisted custom-report base entity.");
        }

        foreach (var column in input.Columns)
        {
            EnsureField(input.BaseEntity, column);
        }

        if (input.Filter is not null)
        {
            ValidateFilterGroup(input.BaseEntity, input.Filter);
        }

        foreach (var groupBy in input.GroupBy)
        {
            EnsureField(input.BaseEntity, groupBy);
        }

        foreach (var aggregate in input.Aggregates)
        {
            EnsureField(input.BaseEntity, aggregate.Field);
        }

        foreach (var sort in input.Sort)
        {
            EnsureField(input.BaseEntity, sort.Field);
        }
    }

    private static void ValidateFilterGroup(string baseEntity, CustomReportFilterGroup group)
    {
        foreach (var filter in group.Filters)
        {
            EnsureField(baseEntity, filter.Field);
        }

        foreach (var nested in group.Groups ?? [])
        {
            ValidateFilterGroup(baseEntity, nested);
        }
    }

    private static void EnsureField(string baseEntity, string field)
    {
        if (!ReportFieldCatalog.IsValidField(baseEntity, field))
        {
            throw new DomainRuleException("FIELD_NOT_WHITELISTED", $"'{field}' is not in the whitelisted field catalog for {baseEntity}.");
        }
    }

    private static CustomReportDefinitionInput Deserialize(ReportDefinition entity) =>
        JsonSerializer.Deserialize<CustomReportDefinitionInput>(entity.DefinitionJson, JsonOptions)!;

    private static ReportDefinitionDto ToDto(ReportDefinition entity, CustomReportDefinitionInput input) =>
        new(entity.Id, entity.Name, entity.BaseEntity, input, entity.Visibility, entity.OwnerId, entity.IsActive);

    private async Task<List<Dictionary<string, object?>>> LoadRowsAsync(Guid tenantId, string baseEntity, ReportScope scope, CancellationToken cancellationToken)
    {
        switch (baseEntity)
        {
            case "Matter":
            {
                var matters = await db.Matters.Where(m => m.TenantId == tenantId).ToListAsync(cancellationToken);
                return matters
                    .Where(m => MatchesScope(scope, m.ResponsibleLawyerId, m.BranchId))
                    .Select(m => new Dictionary<string, object?>
                    {
                        ["Id"] = m.Id,
                        ["Number"] = m.Number,
                        ["Title"] = m.Title,
                        ["Status"] = m.Status,
                        ["Priority"] = m.Priority,
                        ["OpenedOn"] = m.OpenedOn,
                        ["ClosedOn"] = m.ClosedOn,
                        ["PracticeAreaId"] = m.PracticeAreaId,
                        ["ResponsibleLawyerId"] = m.ResponsibleLawyerId,
                        ["ClientId"] = m.ClientId,
                        ["BranchId"] = m.BranchId,
                        ["Budget"] = m.Budget,
                    })
                    .ToList();
            }

            case "TimeEntry":
            {
                var matterBranch = await db.Matters.Where(m => m.TenantId == tenantId).ToDictionaryAsync(m => m.Id, m => m.BranchId, cancellationToken);
                var entries = await db.TimeEntries.Where(t => t.TenantId == tenantId).ToListAsync(cancellationToken);
                return entries
                    .Where(t => MatchesScope(scope, t.UserId, matterBranch.GetValueOrDefault(t.MatterId)))
                    .Select(t => new Dictionary<string, object?>
                    {
                        ["Id"] = t.Id,
                        ["UserId"] = t.UserId,
                        ["MatterId"] = t.MatterId,
                        ["EntryDate"] = t.EntryDate,
                        ["DurationMin"] = t.DurationMin,
                        ["RoundedMin"] = t.RoundedMin,
                        ["Billable"] = t.Billable,
                        ["Status"] = t.Status,
                        ["AmountSnapshot"] = t.AmountSnapshot,
                    })
                    .ToList();
            }

            case "Invoice":
            {
                var matterInfo = await db.Matters.Where(m => m.TenantId == tenantId).ToDictionaryAsync(m => m.Id, m => (m.ResponsibleLawyerId, m.BranchId), cancellationToken);
                var invoices = await db.Invoices.Where(i => i.TenantId == tenantId).ToListAsync(cancellationToken);
                return invoices
                    .Where(i =>
                    {
                        var (lawyerId, branchId) = matterInfo.GetValueOrDefault(i.MatterId);
                        return MatchesScope(scope, lawyerId, branchId);
                    })
                    .Select(i => new Dictionary<string, object?>
                    {
                        ["Id"] = i.Id,
                        ["Number"] = i.Number,
                        ["Status"] = i.Status,
                        ["IssueDate"] = i.IssueDate,
                        ["DueDate"] = i.DueDate,
                        ["GrandTotal"] = i.GrandTotal,
                        ["AmountPaid"] = i.AmountPaid,
                        ["TaxTotal"] = i.TaxTotal,
                        ["ClientId"] = i.ClientId,
                        ["MatterId"] = i.MatterId,
                    })
                    .ToList();
            }

            case "Lead":
            {
                var leads = await db.Leads.Where(l => l.TenantId == tenantId).ToListAsync(cancellationToken);
                return leads
                    .Where(l => MatchesScope(scope, l.OwnerId, l.BranchId))
                    .Select(l => new Dictionary<string, object?>
                    {
                        ["Id"] = l.Id,
                        ["Number"] = l.Number,
                        ["Stage"] = l.Stage,
                        ["Status"] = l.Status,
                        ["OwnerId"] = l.OwnerId,
                        ["BranchId"] = l.BranchId,
                        ["PracticeAreaId"] = l.PracticeAreaId,
                        ["SourceId"] = l.SourceId,
                        ["Score"] = l.Score,
                    })
                    .ToList();
            }

            case "Task":
            {
                var matterBranch = await db.Matters.Where(m => m.TenantId == tenantId).ToDictionaryAsync(m => m.Id, m => m.BranchId, cancellationToken);
                var tasks = await db.OpsTasks.Where(t => t.TenantId == tenantId).ToListAsync(cancellationToken);
                return tasks
                    .Where(t => MatchesScope(scope, t.OwnerId, t.MatterId.HasValue ? matterBranch.GetValueOrDefault(t.MatterId.Value) : null))
                    .Select(t => new Dictionary<string, object?>
                    {
                        ["Id"] = t.Id,
                        ["Title"] = t.Title,
                        ["Status"] = t.Status,
                        ["Priority"] = t.Priority,
                        ["OwnerId"] = t.OwnerId,
                        ["MatterId"] = t.MatterId,
                        ["DueAt"] = t.DueAt,
                        ["ProgressPct"] = t.ProgressPct,
                    })
                    .ToList();
            }

            case "Hearing":
            {
                var caseMatter = await db.CourtCases.Where(c => c.TenantId == tenantId).ToDictionaryAsync(c => c.Id, c => c.MatterId, cancellationToken);
                var matterBranch = await db.Matters.Where(m => m.TenantId == tenantId).ToDictionaryAsync(m => m.Id, m => m.BranchId, cancellationToken);
                var hearings = await db.Hearings.Where(h => h.TenantId == tenantId).ToListAsync(cancellationToken);
                return hearings
                    .Where(h =>
                    {
                        Guid? branchId = caseMatter.TryGetValue(h.CaseId, out var matterId) ? matterBranch.GetValueOrDefault(matterId) : null;
                        return MatchesScope(scope, h.AssignedLawyerId, branchId);
                    })
                    .Select(h => new Dictionary<string, object?>
                    {
                        ["Id"] = h.Id,
                        ["CaseId"] = h.CaseId,
                        ["Date"] = h.Date,
                        ["Status"] = h.Status,
                        ["AssignedLawyerId"] = h.AssignedLawyerId,
                        ["Courtroom"] = h.Courtroom,
                    })
                    .ToList();
            }

            default:
                return [];
        }
    }

    private static bool MatchesScope(ReportScope scope, Guid? lawyerId, Guid? branchId) => ReportScopeMatcher.Matches(scope, lawyerId, branchId);

    internal static ReportResult Build(List<Dictionary<string, object?>> rows, CustomReportDefinitionInput input)
    {
        IEnumerable<Dictionary<string, object?>> filtered = input.Filter is null ? rows : rows.Where(r => Matches(r, input.Filter));

        if (input.GroupBy.Count > 0 || input.Aggregates.Count > 0)
        {
            var columns = input.GroupBy.Concat(input.Aggregates.Select(a => a.Alias ?? $"{a.Function}_{a.Field}")).ToList();
            var groups = filtered
                .GroupBy(r => input.GroupBy.Select(g => r.GetValueOrDefault(g)).ToArray(), new ObjectArrayComparer())
                .Select(g =>
                {
                    var row = new List<object?>(g.Key);
                    row.AddRange(input.Aggregates.Select(a => Aggregate(g, a)));
                    return (IReadOnlyList<object?>)row;
                })
                .ToList();

            return new ReportResult(columns, ApplySort(groups, columns, input.Sort));
        }
        else
        {
            var columns = input.Columns;
            var dataRows = filtered
                .Select(r => (IReadOnlyList<object?>)columns.Select(c => r.GetValueOrDefault(c)).ToList())
                .ToList();

            return new ReportResult(columns, ApplySort(dataRows, columns, input.Sort));
        }
    }

    private static bool Matches(Dictionary<string, object?> row, CustomReportFilterGroup group)
    {
        var leafResults = group.Filters.Select(f => MatchesFilter(row, f));
        var nestedResults = (group.Groups ?? []).Select(g => Matches(row, g));
        var all = leafResults.Concat(nestedResults).ToList();
        if (all.Count == 0)
        {
            return true;
        }

        return string.Equals(group.Logic, "OR", StringComparison.OrdinalIgnoreCase) ? all.Any(x => x) : all.All(x => x);
    }

    private static bool MatchesFilter(Dictionary<string, object?> row, CustomReportFilter filter)
    {
        var value = row.GetValueOrDefault(filter.Field);
        return filter.Operator.ToLowerInvariant() switch
        {
            "eq" => string.Equals(Stringify(value), filter.Value, StringComparison.OrdinalIgnoreCase),
            "neq" => !string.Equals(Stringify(value), filter.Value, StringComparison.OrdinalIgnoreCase),
            "contains" => value is not null && filter.Value is not null && Stringify(value).Contains(filter.Value, StringComparison.OrdinalIgnoreCase),
            "gt" => Compare(value, filter.Value) > 0,
            "gte" => Compare(value, filter.Value) >= 0,
            "lt" => Compare(value, filter.Value) < 0,
            "lte" => Compare(value, filter.Value) <= 0,
            _ => true,
        };
    }

    private static object? Aggregate(IEnumerable<Dictionary<string, object?>> group, CustomReportAggregate aggregate)
    {
        var values = group.Select(r => ToDecimal(r.GetValueOrDefault(aggregate.Field))).ToList();
        return aggregate.Function.ToLowerInvariant() switch
        {
            "sum" => values.Sum(v => v ?? 0),
            "avg" => values.Count > 0 ? values.Where(v => v.HasValue).Select(v => v!.Value).DefaultIfEmpty(0).Average() : 0,
            "count" => group.Count(),
            "min" => values.Where(v => v.HasValue).Select(v => v!.Value).DefaultIfEmpty(0).Min(),
            "max" => values.Where(v => v.HasValue).Select(v => v!.Value).DefaultIfEmpty(0).Max(),
            _ => null,
        };
    }

    private static List<IReadOnlyList<object?>> ApplySort(List<IReadOnlyList<object?>> rows, IReadOnlyList<string> columns, IReadOnlyList<CustomReportSort> sort)
    {
        if (sort.Count == 0)
        {
            return rows;
        }

        IOrderedEnumerable<IReadOnlyList<object?>>? ordered = null;
        foreach (var s in sort)
        {
            var index = columns.ToList().IndexOf(s.Field);
            if (index < 0)
            {
                continue;
            }

            ordered = ordered is null
                ? (s.Descending ? rows.OrderByDescending(r => SortKey(r[index])) : rows.OrderBy(r => SortKey(r[index])))
                : (s.Descending ? ordered.ThenByDescending(r => SortKey(r[index])) : ordered.ThenBy(r => SortKey(r[index])));
        }

        return ordered?.ToList() ?? rows;
    }

    private static IComparable SortKey(object? value) => ToDecimal(value) ?? (IComparable)(Stringify(value));

    private static string Stringify(object? value) => value switch
    {
        null => string.Empty,
        DateOnly d => d.ToString("yyyy-MM-dd"),
        DateTimeOffset d => d.ToString("O"),
        _ => value.ToString() ?? string.Empty,
    };

    private static decimal? ToDecimal(object? value) => value switch
    {
        null => null,
        decimal d => d,
        int i => i,
        long l => l,
        bool b => b ? 1 : 0,
        DateOnly d => d.DayNumber,
        DateTimeOffset d => d.UtcTicks,
        _ => decimal.TryParse(value.ToString(), out var parsed) ? parsed : null,
    };

    private static int Compare(object? value, string? filterValue)
    {
        var left = ToDecimal(value);
        if (left.HasValue && decimal.TryParse(filterValue, out var right))
        {
            return left.Value.CompareTo(right);
        }

        return string.Compare(Stringify(value), filterValue, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ObjectArrayComparer : IEqualityComparer<object?[]>
    {
        public bool Equals(object?[]? x, object?[]? y)
        {
            if (x is null || y is null)
            {
                return x == y;
            }

            return x.Length == y.Length && x.Zip(y).All(pair => Stringify(pair.First) == Stringify(pair.Second));
        }

        public int GetHashCode(object?[] obj) => obj.Aggregate(17, (hash, v) => hash * 31 + Stringify(v).GetHashCode());
    }
}
