namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 13: the 12 standard reports, run as parameterized queries against the rpt.* star schema
/// (heavy aggregates: Revenue, Cases/Matters, Lawyer Performance, Practice Area) or directly
/// against the owning OLTP schema for lighter, current-state/list reports (Clients, Expenses,
/// Billing, Payments, Tasks, Hearings, Lead Conversion, Trust) — see StandardReportService's own
/// doc comment for the full per-report source mapping and why the split is deliberate.
/// </summary>
public interface IStandardReportService
{
    IReadOnlyList<ReportCatalogItem> GetCatalog();

    Task<ReportResult> RunAsync(Guid tenantId, string reportKey, ReportRunParams reportParams, ReportScope scope, CancellationToken cancellationToken = default);
}

public sealed record ReportCatalogItem(string Key, string Name, string Category, bool RequiresFinancialPermission);

/// <summary>Module 13 Validation: "Date range ≤ 3 years per run."</summary>
public sealed record ReportRunParams(DateOnly? DateFrom, DateOnly? DateTo, Guid? PracticeAreaId, Guid? LawyerId, Guid? ClientId, Guid? BranchId);

/// <summary>A flat, exportable tabular result — the common currency between standard reports, the custom builder, and every export format.</summary>
public sealed record ReportResult(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<object?>> Rows)
{
    public int RowCount => Rows.Count;
}
