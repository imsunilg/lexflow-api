namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 8: payments + allocations (§19.8: over-allocation impossible), credit notes, refunds,
/// and gateway webhook capture (payment.captured -&gt; allocate -&gt; mark paid, exactly-once via the
/// gateway event-id store — see IWebhookRouter/core.webhook_events, reused from §34).
/// </summary>
public interface IPaymentService
{
    /// <summary>AC-B3: idempotent by (tenant, idempotencyKey) — a duplicate call with the same key returns the original result and makes no second entry (409 IDEMPOTENT_REPLAY at the API layer; here it's a plain replay-safe return).</summary>
    Task<PaymentDto> RecordPaymentAsync(Guid tenantId, RecordPaymentInput input, CancellationToken cancellationToken = default);

    /// <summary>Gateway webhook path: payment.captured -&gt; find-or-create the Payment row keyed by (gateway, gatewayRef) -&gt; allocate to the named invoice -&gt; mark invoice Paid/PartiallyPaid. Exactly-once is guaranteed by the caller (WebhookRouter's core.webhook_events dedupe), not re-checked here.</summary>
    Task<PaymentDto> HandleGatewayCapturedAsync(Guid tenantId, string gateway, string gatewayRef, decimal amount, Guid invoiceId, CancellationToken cancellationToken = default);

    Task<PaymentDto?> GetAsync(Guid tenantId, Guid paymentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentDto>> GetForClientAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default);

    Task<CreditNoteDto> CreateCreditNoteAsync(Guid tenantId, Guid invoiceId, decimal amount, string reason, CancellationToken cancellationToken = default);

    /// <summary>Applies an Issued credit note against its invoice's open balance (Module 8 Edge Cases: "credit applies to open balance only").</summary>
    Task<CreditNoteDto> ApplyCreditNoteAsync(Guid tenantId, Guid creditNoteId, CancellationToken cancellationToken = default);

    Task<RefundDto> CreateRefundAsync(Guid tenantId, Guid paymentId, decimal amount, string? reason, CancellationToken cancellationToken = default);

    Task<RefundDto> MarkRefundProcessedAsync(Guid tenantId, Guid refundId, string? gatewayRef, CancellationToken cancellationToken = default);

    Task<ClientStatementDto> GetStatementAsync(Guid tenantId, Guid clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

public sealed record RecordPaymentInput(Guid ClientId, decimal Amount, string Mode, string? Gateway, string? GatewayRef, DateOnly ReceivedOn, IReadOnlyList<PaymentAllocationInput> Allocations, string? IdempotencyKey);

public sealed record PaymentAllocationInput(Guid InvoiceId, decimal Amount);

public sealed record PaymentDto(Guid Id, string ReceiptNumber, Guid ClientId, decimal Amount, string Mode, string? Gateway, string? GatewayRef, DateOnly ReceivedOn, string Status, IReadOnlyList<PaymentAllocationDto> Allocations);

public sealed record PaymentAllocationDto(Guid InvoiceId, decimal Amount);

public sealed record CreditNoteDto(Guid Id, string Number, Guid InvoiceId, decimal Amount, string Reason, string Status);

public sealed record RefundDto(Guid Id, Guid PaymentId, decimal Amount, string? Reason, string Status, string? GatewayRef);

public sealed record ClientStatementDto(Guid ClientId, DateOnly From, DateOnly To, IReadOnlyList<InvoiceDto> Invoices, IReadOnlyList<PaymentDto> Payments, decimal OpeningBalance, decimal ClosingBalance);
