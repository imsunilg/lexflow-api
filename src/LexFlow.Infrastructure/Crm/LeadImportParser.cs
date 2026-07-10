using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;

namespace LexFlow.Infrastructure.Crm;

/// <summary>Parses the CSV/XLSX import file into raw rows for <see cref="LeadImportService"/> to validate/persist. Expected columns (case-insensitive): FirstName, LastName, Company, Email, Phone, IssueSummary.</summary>
internal static class LeadImportParser
{
    public static IReadOnlyList<ImportRow> Parse(string fileName, byte[] content)
        => fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ? ParseXlsx(content) : ParseCsv(content);

    private static IReadOnlyList<ImportRow> ParseCsv(byte[] content)
    {
        var rows = new List<ImportRow>();
        using var stream = new MemoryStream(content);
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        if (!csv.Read() || !csv.ReadHeader())
        {
            return rows;
        }

        var rowNumber = 1;
        while (csv.Read())
        {
            rowNumber++;
            rows.Add(new ImportRow(
                rowNumber,
                GetField(csv, "FirstName"),
                GetField(csv, "LastName"),
                GetField(csv, "Company"),
                GetField(csv, "Email"),
                GetField(csv, "Phone"),
                GetField(csv, "IssueSummary"),
                RawLine: string.Join(',', csv.Parser.Record ?? [])));
        }

        return rows;
    }

    private static string? GetField(CsvReader csv, string name) => csv.TryGetField<string>(name, out var value) ? value : null;

    private static IReadOnlyList<ImportRow> ParseXlsx(byte[] content)
    {
        var rows = new List<ImportRow>();
        using var stream = new MemoryStream(content);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();

        var headerRow = sheet.Row(1);
        var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            columnIndex[cell.GetString().Trim()] = cell.Address.ColumnNumber;
        }

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= lastRow; r++)
        {
            var row = sheet.Row(r);
            if (row.IsEmpty())
            {
                continue;
            }

            string? Field(string column) => columnIndex.TryGetValue(column, out var col) ? row.Cell(col).GetString() : null;

            rows.Add(new ImportRow(
                r,
                Field("FirstName"),
                Field("LastName"),
                Field("Company"),
                Field("Email"),
                Field("Phone"),
                Field("IssueSummary"),
                RawLine: string.Join(',', headerRow.CellsUsed().Select(c => Field(c.GetString()) ?? ""))));
        }

        return rows;
    }
}

internal sealed record ImportRow(int RowNumber, string? FirstName, string? LastName, string? Company, string? Email, string? Phone, string? IssueSummary, string RawLine);
