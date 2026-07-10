namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 13: "PDF via server-side render [QuestPDF], XLSX via ClosedXML with typed columns, CSV
/// UTF-8 BOM." Report layouts: "firm letterhead header, filter echo line, generated-at timestamp
/// + TZ, page numbers, totals row." Validation: "row cap 100k per export (larger -&gt; async + email
/// link)" — the row-cap decision is made by the caller (IReportRunService); this service only renders.
/// </summary>
public interface IReportExportService
{
    byte[] Render(ReportResult result, ReportExportContext context, string format);
}

public sealed record ReportExportContext(string ReportTitle, string TenantName, string FilterEcho, DateTimeOffset GeneratedAt, string TimeZoneLabel);
