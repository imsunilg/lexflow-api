using System.Text;
using FluentAssertions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Reporting;

namespace LexFlow.UnitTests.Reporting;

/// <summary>Module 13 Report layouts: "PDF via server-side render [QuestPDF], XLSX via ClosedXML with typed columns, CSV UTF-8 BOM."</summary>
public sealed class ReportExportServiceTests
{
    private static readonly ReportResult Result = new(["Name", "Amount"], [["Alpha", 100m], ["Beta", 200m]]);
    private static readonly ReportExportContext Context = new("Revenue", "Test Firm", "branch=HQ", DateTimeOffset.UtcNow, "IST");

    [Fact]
    public void Render_pdf_produces_a_non_empty_pdf_document()
    {
        var service = new ReportExportService();

        var bytes = service.Render(Result, Context, "pdf");

        bytes.Should().NotBeEmpty();
        Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public void Render_xlsx_produces_a_non_empty_workbook()
    {
        var service = new ReportExportService();

        var bytes = service.Render(Result, Context, "xlsx");

        bytes.Should().NotBeEmpty();
        // XLSX is a zip container — starts with the local file header signature "PK".
        bytes[0].Should().Be((byte)'P');
        bytes[1].Should().Be((byte)'K');
    }

    [Fact]
    public void Render_csv_starts_with_a_utf8_bom_and_includes_the_header_row()
    {
        var service = new ReportExportService();

        var bytes = service.Render(Result, Context, "csv");

        var bom = Encoding.UTF8.GetPreamble();
        bytes.Take(bom.Length).Should().Equal(bom);

        var text = Encoding.UTF8.GetString(bytes, bom.Length, bytes.Length - bom.Length);
        text.Should().StartWith("Name,Amount");
        text.Should().Contain("Alpha,100.00");
    }

    [Fact]
    public void Render_throws_for_an_unsupported_format()
    {
        var service = new ReportExportService();

        var act = () => service.Render(Result, Context, "docx");

        act.Should().Throw<NotSupportedException>();
    }
}
