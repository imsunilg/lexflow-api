using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using LexFlow.Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LexFlow.Infrastructure.Reporting;

/// <summary>
/// Module 13: "PDF via server-side render [QuestPDF], XLSX via ClosedXML with typed columns, CSV
/// UTF-8 BOM." Report layouts: "firm letterhead header, filter echo line, generated-at timestamp
/// + TZ, page numbers, totals row." The totals row only sums columns whose values are all numeric
/// — text/date columns are left blank in that row rather than mislabeled with a spurious 0.
/// </summary>
public sealed class ReportExportService : IReportExportService
{
    static ReportExportService() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Render(ReportResult result, ReportExportContext context, string format) => format.ToLowerInvariant() switch
    {
        "pdf" => RenderPdf(result, context),
        "xlsx" => RenderXlsx(result, context),
        "csv" => RenderCsv(result),
        _ => throw new NotSupportedException($"Unsupported export format '{format}'."),
    };

    private static byte[] RenderPdf(ReportResult result, ReportExportContext context)
    {
        var totals = ComputeTotals(result);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Column(col =>
                {
                    col.Item().Text(context.TenantName).FontSize(14).Bold();
                    col.Item().Text(context.ReportTitle).FontSize(12).Bold();
                    if (!string.IsNullOrWhiteSpace(context.FilterEcho))
                    {
                        col.Item().Text($"Filters: {context.FilterEcho}").FontSize(8);
                    }

                    col.Item().Text($"Generated: {context.GeneratedAt:yyyy-MM-dd HH:mm} ({context.TimeZoneLabel})").FontSize(8);
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in result.Columns)
                        {
                            columns.RelativeColumn();
                        }
                    });

                    table.Header(header =>
                    {
                        foreach (var column in result.Columns)
                        {
                            header.Cell().Text(column).Bold();
                        }
                    });

                    foreach (var row in result.Rows)
                    {
                        foreach (var cell in row)
                        {
                            table.Cell().Text(FormatCell(cell));
                        }
                    }

                    foreach (var column in result.Columns)
                    {
                        table.Cell().Text(totals.TryGetValue(column, out var total) ? $"Total: {total:0.00}" : string.Empty).Bold();
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static byte[] RenderXlsx(ReportResult result, ReportExportContext context)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Report");

        sheet.Cell(1, 1).Value = context.TenantName;
        sheet.Cell(2, 1).Value = context.ReportTitle;
        sheet.Cell(3, 1).Value = context.FilterEcho;
        sheet.Cell(4, 1).Value = $"Generated: {context.GeneratedAt:yyyy-MM-dd HH:mm} ({context.TimeZoneLabel})";

        const int headerRow = 6;
        for (var c = 0; c < result.Columns.Count; c++)
        {
            var cell = sheet.Cell(headerRow, c + 1);
            cell.Value = result.Columns[c];
            cell.Style.Font.Bold = true;
        }

        var rowIndex = headerRow + 1;
        foreach (var row in result.Rows)
        {
            for (var c = 0; c < row.Count; c++)
            {
                SetTypedCell(sheet.Cell(rowIndex, c + 1), row[c]);
            }

            rowIndex++;
        }

        var totals = ComputeTotals(result);
        for (var c = 0; c < result.Columns.Count; c++)
        {
            if (totals.TryGetValue(result.Columns[c], out var total))
            {
                var cell = sheet.Cell(rowIndex, c + 1);
                cell.Value = total;
                cell.Style.Font.Bold = true;
            }
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void SetTypedCell(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                cell.Value = string.Empty;
                break;
            case decimal d:
                cell.Value = d;
                break;
            case int i:
                cell.Value = i;
                break;
            case bool b:
                cell.Value = b;
                break;
            case DateOnly d:
                cell.Value = d.ToDateTime(TimeOnly.MinValue);
                break;
            case DateTimeOffset d:
                cell.Value = d.UtcDateTime;
                break;
            default:
                cell.Value = value.ToString();
                break;
        }
    }

    private static byte[] RenderCsv(ReportResult result)
    {
        using var stream = new MemoryStream();
        // UTF-8 BOM per Module 13 Report layouts: "CSV UTF-8 BOM."
        stream.Write(Encoding.UTF8.GetPreamble());

        using (var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            foreach (var column in result.Columns)
            {
                csv.WriteField(column);
            }

            csv.NextRecord();

            foreach (var row in result.Rows)
            {
                foreach (var cell in row)
                {
                    csv.WriteField(FormatCell(cell));
                }

                csv.NextRecord();
            }
        }

        return stream.ToArray();
    }

    private static string FormatCell(object? value) => value switch
    {
        null => string.Empty,
        decimal d => d.ToString("0.00", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd"),
        DateTimeOffset d => d.ToString("yyyy-MM-dd HH:mm"),
        _ => value.ToString() ?? string.Empty,
    };

    private static Dictionary<string, decimal> ComputeTotals(ReportResult result)
    {
        var totals = new Dictionary<string, decimal>();
        for (var c = 0; c < result.Columns.Count; c++)
        {
            var index = c;
            var values = result.Rows.Select(r => r[index]).ToList();
            if (values.Count == 0)
            {
                continue;
            }

            if (values.All(v => v is decimal or int or long))
            {
                totals[result.Columns[c]] = values.Sum(v => Convert.ToDecimal(v));
            }
        }

        return totals;
    }
}
