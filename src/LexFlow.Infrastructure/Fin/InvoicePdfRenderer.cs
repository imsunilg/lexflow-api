using LexFlow.Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LexFlow.Infrastructure.Fin;

/// <summary>Module 8 User Flow #5: "PDF (firm-branded template, number series ...)." QuestPDF Community license — free for this project's revenue profile per QuestPDF's own licensing terms.</summary>
public sealed class InvoicePdfRenderer : IInvoicePdfRenderer
{
    static InvoicePdfRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Render(InvoicePdfModel model)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(model.FirmName).FontSize(18).Bold();
                        if (model.FirmAddress is not null)
                        {
                            col.Item().Text(model.FirmAddress);
                        }

                        if (model.FirmGstin is not null)
                        {
                            col.Item().Text($"GSTIN: {model.FirmGstin}");
                        }
                    });

                    row.ConstantItem(120).Column(col =>
                    {
                        col.Item().AlignRight().Text("TAX INVOICE").Bold();
                        col.Item().AlignRight().Text(model.InvoiceNumber);
                        if (model.IssueDate.HasValue)
                        {
                            col.Item().AlignRight().Text($"Date: {model.IssueDate:yyyy-MM-dd}");
                        }

                        if (model.DueDate.HasValue)
                        {
                            col.Item().AlignRight().Text($"Due: {model.DueDate:yyyy-MM-dd}");
                        }
                    });
                });

                page.Content().Column(col =>
                {
                    col.Item().PaddingVertical(10).Column(billTo =>
                    {
                        billTo.Item().Text("Bill To").Bold();
                        billTo.Item().Text(model.ClientDisplayName);
                        if (model.ClientGstin is not null)
                        {
                            billTo.Item().Text($"GSTIN: {model.ClientGstin}");
                        }
                    });

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(4);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.5f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Description").Bold();
                            header.Cell().Text("Qty").Bold();
                            header.Cell().Text("Unit").Bold();
                            header.Cell().Text("Rate").Bold();
                            header.Cell().Text("Amount").Bold();
                        });

                        foreach (var line in model.Lines)
                        {
                            table.Cell().Text(line.Description);
                            table.Cell().Text(line.Qty.ToString("0.00"));
                            table.Cell().Text(line.Unit ?? string.Empty);
                            table.Cell().Text(line.Rate.ToString("0.00"));
                            table.Cell().Text(line.Amount.ToString("0.00"));
                        }
                    });

                    col.Item().PaddingTop(10).AlignRight().Column(totals =>
                    {
                        totals.Item().Text($"Sub Total: {model.Currency} {model.SubTotal:0.00}");
                        if (model.DiscountTotal > 0)
                        {
                            totals.Item().Text($"Discount: -{model.Currency} {model.DiscountTotal:0.00}");
                        }

                        foreach (var tax in model.Taxes)
                        {
                            totals.Item().Text($"{tax.Name} ({tax.RatePct}%): {model.Currency} {tax.Amount:0.00}");
                        }

                        totals.Item().PaddingTop(5).Text($"Grand Total: {model.Currency} {model.GrandTotal:0.00}").Bold();
                    });

                    if (!string.IsNullOrWhiteSpace(model.Notes))
                    {
                        col.Item().PaddingTop(10).Text(model.Notes);
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Generated by LexFlow").FontSize(8);
                });
            });
        });

        return document.GeneratePdf();
    }
}
