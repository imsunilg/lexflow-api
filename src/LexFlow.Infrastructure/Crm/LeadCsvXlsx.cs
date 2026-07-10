using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using LexFlow.Application.Common.Interfaces;

namespace LexFlow.Infrastructure.Crm;

/// <summary>CSV/XLSX (de)serialization shared by lead export (GET /leads/export) and the import pipeline's error-row report.</summary>
internal static class LeadCsvXlsx
{
    private static readonly string[] Headers =
        ["Number", "FirstName", "LastName", "Company", "Email", "Phone", "Stage", "Status", "Score", "CreatedAt"];

    public static byte[] ToCsv(IReadOnlyList<LeadDto> leads)
    {
        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, leaveOpen: true))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            foreach (var header in Headers)
            {
                csv.WriteField(header);
            }

            csv.NextRecord();

            foreach (var lead in leads)
            {
                csv.WriteField(lead.Number);
                csv.WriteField(lead.FirstName);
                csv.WriteField(lead.LastName);
                csv.WriteField(lead.Company);
                csv.WriteField(lead.Email);
                csv.WriteField(lead.PhoneE164);
                csv.WriteField(lead.Stage);
                csv.WriteField(lead.Status);
                csv.WriteField(lead.Score);
                csv.WriteField(lead.CreatedAt.ToString("O"));
                csv.NextRecord();
            }
        }

        return stream.ToArray();
    }

    public static byte[] ToXlsx(IReadOnlyList<LeadDto> leads)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Leads");

        for (var i = 0; i < Headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = Headers[i];
        }

        var row = 2;
        foreach (var lead in leads)
        {
            sheet.Cell(row, 1).Value = lead.Number;
            sheet.Cell(row, 2).Value = lead.FirstName;
            sheet.Cell(row, 3).Value = lead.LastName;
            sheet.Cell(row, 4).Value = lead.Company;
            sheet.Cell(row, 5).Value = lead.Email;
            sheet.Cell(row, 6).Value = lead.PhoneE164;
            sheet.Cell(row, 7).Value = lead.Stage;
            sheet.Cell(row, 8).Value = lead.Status;
            sheet.Cell(row, 9).Value = lead.Score;
            sheet.Cell(row, 10).Value = lead.CreatedAt.UtcDateTime;
            row++;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>Error-CSV for the import pipeline (Module 2 Error Handling: "invalid rows returned as downloadable error CSV").</summary>
    public static byte[] ToErrorCsv(IReadOnlyList<(int RowNumber, string RawRow, string Error)> rows)
    {
        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, leaveOpen: true))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            csv.WriteField("RowNumber");
            csv.WriteField("RawRow");
            csv.WriteField("Error");
            csv.NextRecord();

            foreach (var row in rows)
            {
                csv.WriteField(row.RowNumber);
                csv.WriteField(row.RawRow);
                csv.WriteField(row.Error);
                csv.NextRecord();
            }
        }

        return stream.ToArray();
    }
}
