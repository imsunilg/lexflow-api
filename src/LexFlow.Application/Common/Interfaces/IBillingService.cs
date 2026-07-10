namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 8: invoice lifecycle (WIP pull -&gt; draft -&gt; submit -&gt; approve/reject -&gt; send -&gt; paid/void),
/// the batch-billing engine, and BR-9 GST place-of-supply tax calculation. BR-4 (immutability once
/// non-Draft) is enforced first at the DB (fin.invoices/invoice_lines/invoice_taxes 004_Triggers.sql)
/// and again defensively in Invoice's own mutators; DbUpdateException carrying the trigger's BR-4
/// text is translated here to DomainRuleException("INVOICE_NOT_DRAFT", ...).
/// </summary>
public interface IBillingService
{
    /// <summary>Manual invoice creation: pulls specified unbilled time entries/expenses as lines, plus any extra fixed/retainer lines, computes tax per BR-9, and creates a Draft invoice. AC-B1's totals-to-the-paisa guarantee starts here.</summary>
    Task<InvoiceDto> CreateDraftAsync(Guid tenantId, Guid matterId, CreateInvoiceInput input, CancellationToken cancellationToken = default);

    /// <summary>Batch billing engine (AC-B1): "bill all matters with WIP > X as of asOf" — pulls WIP (approved unbilled time + Fixed milestones due + Retainer lines due) per matching matter and creates one Draft invoice per matter.</summary>
    Task<IReadOnlyList<InvoiceDto>> CreateBatchAsync(Guid tenantId, BatchBillingFilter filter, DateOnly asOf, CancellationToken cancellationToken = default);

    Task<InvoiceDto?> GetAsync(Guid tenantId, Guid invoiceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvoiceDto>> ListAsync(Guid tenantId, InvoiceFilter filter, CancellationToken cancellationToken = default);

    Task<InvoiceDto> UpdateDraftAsync(Guid tenantId, Guid invoiceId, UpdateInvoiceInput input, CancellationToken cancellationToken = default);

    /// <summary>Module 8 User Flow #4: "if amount > threshold or write-down > Y%, approval workflow (partner) before send." Submit is a no-op pass-through to Approved when the invoice is under threshold (AutoApproveIfBelowThreshold below decides that at the caller/handler level via config read).</summary>
    Task<InvoiceDto> SubmitAsync(Guid tenantId, Guid actorId, Guid invoiceId, CancellationToken cancellationToken = default);

    Task<InvoiceDto> ApproveAsync(Guid tenantId, Guid actorId, Guid invoiceId, CancellationToken cancellationToken = default);

    Task<InvoiceDto> RejectAsync(Guid tenantId, Guid actorId, Guid invoiceId, string reason, CancellationToken cancellationToken = default);

    /// <summary>Assigns the number-series number (BR-14, first send only), renders the PDF, and marks Sent. Error Handling: "PDF render failure -> send blocked with error (never send without attachment)."</summary>
    Task<InvoiceDto> SendAsync(Guid tenantId, Guid actorId, Guid invoiceId, CancellationToken cancellationToken = default);

    /// <summary>AC-B5: void only if unpaid; voided numbers are never reused.</summary>
    Task<InvoiceDto> VoidAsync(Guid tenantId, Guid actorId, Guid invoiceId, string reason, CancellationToken cancellationToken = default);

    Task<byte[]> RenderPdfAsync(Guid tenantId, Guid invoiceId, CancellationToken cancellationToken = default);

    /// <summary>AC-B6: aging buckets sum to total AR exactly.</summary>
    Task<AgingReportDto> GetAgingAsync(Guid tenantId, DateOnly asOf, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvoiceStatusHistoryDto>> GetStatusHistoryAsync(Guid tenantId, Guid invoiceId, CancellationToken cancellationToken = default);
}

public sealed record CreateInvoiceInput(DateOnly? IssueDate, int DueInDays, IReadOnlyList<Guid>? PullTimeEntryIds, IReadOnlyList<ExtraLineInput>? ExtraLines, DiscountInput? Discount, string? Notes);

public sealed record ExtraLineInput(string Type, string? Description, decimal Qty, string? Unit, decimal Rate);

public sealed record DiscountInput(string Type, decimal Value);

public sealed record UpdateInvoiceInput(DateOnly? IssueDate, DateOnly? DueDate, string? Notes);

public sealed record BatchBillingFilter(decimal MinWip, Guid? BranchId, Guid? MatterTypeId);

public sealed record InvoiceFilter(string? Status, Guid? ClientId, Guid? MatterId, DateOnly? From, DateOnly? To, bool? OverdueOnly);

public sealed record InvoiceDto(
    Guid Id, string Number, Guid MatterId, Guid ClientId, string Status, DateOnly? IssueDate, DateOnly? DueDate,
    string Currency, decimal SubTotal, decimal DiscountTotal, decimal TaxTotal, decimal GrandTotal, decimal AmountPaid,
    string? Notes, string? PdfBlobPath, IReadOnlyList<InvoiceLineDto> Lines, IReadOnlyList<InvoiceTaxDto> Taxes);

public sealed record InvoiceLineDto(Guid Id, int LineNo, string Type, string? Description, decimal Qty, string? Unit, decimal Rate, decimal Amount, IReadOnlyList<Guid> TimeEntryIds);

public sealed record InvoiceTaxDto(string Name, decimal RatePct, decimal TaxableAmount, decimal Amount);

public sealed record AgingReportDto(decimal Current, decimal Bucket1To30, decimal Bucket31To60, decimal Bucket61To90, decimal Over90, decimal Total);

public sealed record InvoiceStatusHistoryDto(string? FromStatus, string ToStatus, string? Reason, Guid? ChangedBy, DateTimeOffset ChangedAt);
