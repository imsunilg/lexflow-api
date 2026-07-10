namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 8 User Flow #5: "PDF (firm-branded template, number series ...)." Firm branding is read from core.tenant_settings key "branding" (Module 15 Settings §2), never hardcoded.</summary>
public interface IInvoicePdfRenderer
{
    byte[] Render(InvoicePdfModel model);
}

public sealed record InvoicePdfModel(
    string FirmName, string? FirmLogoDataUri, string? FirmAddress, string? FirmGstin,
    string InvoiceNumber, DateOnly? IssueDate, DateOnly? DueDate, string Currency,
    string ClientDisplayName, string? ClientGstin, string? ClientBillingAddress,
    IReadOnlyList<InvoicePdfLine> Lines, IReadOnlyList<InvoicePdfTax> Taxes,
    decimal SubTotal, decimal DiscountTotal, decimal TaxTotal, decimal GrandTotal, string? Notes);

public sealed record InvoicePdfLine(string Description, decimal Qty, string? Unit, decimal Rate, decimal Amount);

public sealed record InvoicePdfTax(string Name, decimal RatePct, decimal Amount);
