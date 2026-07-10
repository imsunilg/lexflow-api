using System.Data;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Fin;

/// <summary>
/// Module 8: payments + allocations (§19.8: "over-allocation impossible (Σallocations ≤
/// payment.amount and per-invoice Σ ≤ open balance, enforced in serializable transaction)" — no DB
/// trigger backs fin.payment_allocations, so this application-level check plus a serializable
/// transaction under Postgres is the sole enforcement, unlike trust/invoice which also have a DB
/// backstop), credit notes, refunds, and gateway-captured payment ingestion (AC-B3).
/// </summary>
public sealed class PaymentService(LexFlowDbContext db) : IPaymentService
{
    public async Task<PaymentDto> RecordPaymentAsync(Guid tenantId, RecordPaymentInput input, CancellationToken cancellationToken = default)
    {
        if (input.IdempotencyKey is not null)
        {
            var existing = await db.Payments.SingleOrDefaultAsync(p => p.TenantId == tenantId && p.IdempotencyKey == input.IdempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return await BuildDtoAsync(existing, cancellationToken);
            }
        }

        var allocationSum = input.Allocations.Sum(a => a.Amount);
        if (allocationSum > input.Amount)
        {
            throw new DomainRuleException("OVER_ALLOCATION", $"Allocations totalling {allocationSum} exceed the payment amount of {input.Amount} (§19.8).");
        }

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        var receiptNumber = await GenerateReceiptNumberAsync(tenantId, cancellationToken);
        var payment = new Payment(tenantId, receiptNumber, input.ClientId, input.Amount, input.Mode, input.Gateway, input.GatewayRef, input.ReceivedOn, input.IdempotencyKey);
        payment.MarkCleared();
        await db.Payments.AddAsync(payment, cancellationToken);

        foreach (var allocation in input.Allocations)
        {
            var invoice = await db.Invoices.SingleOrDefaultAsync(i => i.TenantId == tenantId && i.Id == allocation.InvoiceId, cancellationToken)
                ?? throw new NotFoundException(nameof(Invoice), allocation.InvoiceId);

            var openBalance = invoice.GrandTotal - invoice.AmountPaid;
            if (allocation.Amount > openBalance)
            {
                throw new DomainRuleException("OVER_ALLOCATION", $"Allocation of {allocation.Amount} to invoice {invoice.Id} exceeds its open balance of {openBalance} (§19.8).");
            }

            await db.PaymentAllocations.AddAsync(new PaymentAllocation(tenantId, payment.Id, invoice.Id, allocation.Amount), cancellationToken);
            invoice.ApplyPayment(allocation.Amount);
        }

        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return await BuildDtoAsync(payment, cancellationToken);
    }

    public async Task<PaymentDto> HandleGatewayCapturedAsync(Guid tenantId, string gateway, string gatewayRef, decimal amount, Guid invoiceId, CancellationToken cancellationToken = default)
    {
        // Exactly-once is primarily guaranteed by the caller (WebhookRouter's core.webhook_events
        // dedupe per gateway event id, §34); this (gateway, gatewayRef) check is a second,
        // independent safety net specific to payment capture (AC-B3: "duplicate webhook makes no
        // double entry").
        var existing = await db.Payments.SingleOrDefaultAsync(p => p.TenantId == tenantId && p.Gateway == gateway && p.GatewayRef == gatewayRef, cancellationToken);
        if (existing is not null)
        {
            return await BuildDtoAsync(existing, cancellationToken);
        }

        var invoice = await db.Invoices.SingleOrDefaultAsync(i => i.TenantId == tenantId && i.Id == invoiceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Invoice), invoiceId);

        var receiptNumber = await GenerateReceiptNumberAsync(tenantId, cancellationToken);
        var payment = new Payment(tenantId, receiptNumber, invoice.ClientId, amount, "Gateway", gateway, gatewayRef, DateOnly.FromDateTime(DateTime.UtcNow), null);
        payment.MarkCleared();
        await db.Payments.AddAsync(payment, cancellationToken);

        var openBalance = invoice.GrandTotal - invoice.AmountPaid;
        var allocationAmount = Math.Min(amount, Math.Max(0, openBalance));
        if (allocationAmount > 0)
        {
            await db.PaymentAllocations.AddAsync(new PaymentAllocation(tenantId, payment.Id, invoice.Id, allocationAmount), cancellationToken);
            invoice.ApplyPayment(allocationAmount);
        }

        await db.SaveChangesAsync(cancellationToken);
        return await BuildDtoAsync(payment, cancellationToken);
    }

    public async Task<PaymentDto?> GetAsync(Guid tenantId, Guid paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await db.Payments.SingleOrDefaultAsync(p => p.TenantId == tenantId && p.Id == paymentId, cancellationToken);
        return payment is null ? null : await BuildDtoAsync(payment, cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentDto>> GetForClientAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default)
    {
        var payments = await db.Payments.Where(p => p.TenantId == tenantId && p.ClientId == clientId).OrderByDescending(p => p.ReceivedOn).ToListAsync(cancellationToken);
        var results = new List<PaymentDto>();
        foreach (var payment in payments)
        {
            results.Add(await BuildDtoAsync(payment, cancellationToken));
        }

        return results;
    }

    public async Task<CreditNoteDto> CreateCreditNoteAsync(Guid tenantId, Guid invoiceId, decimal amount, string reason, CancellationToken cancellationToken = default)
    {
        var invoice = await db.Invoices.SingleOrDefaultAsync(i => i.TenantId == tenantId && i.Id == invoiceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Invoice), invoiceId);

        var number = await GenerateSequencedNumberAsync(tenantId, "CN", cancellationToken);
        var note = new CreditNote(tenantId, number, invoiceId, amount, reason);
        note.Issue();
        await db.CreditNotes.AddAsync(note, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(note);
    }

    public async Task<CreditNoteDto> ApplyCreditNoteAsync(Guid tenantId, Guid creditNoteId, CancellationToken cancellationToken = default)
    {
        var note = await db.CreditNotes.SingleOrDefaultAsync(c => c.TenantId == tenantId && c.Id == creditNoteId, cancellationToken)
            ?? throw new NotFoundException(nameof(CreditNote), creditNoteId);

        if (note.Status != "Issued")
        {
            throw new ConflictException($"Credit note {creditNoteId} must be Issued before it can be applied (current status {note.Status}).", "CREDIT_NOTE_NOT_ISSUED");
        }

        var invoice = await db.Invoices.SingleAsync(i => i.Id == note.InvoiceId, cancellationToken);

        // Module 8 Edge Cases: "credit applies to open balance only" — never overshoots.
        var openBalance = invoice.GrandTotal - invoice.AmountPaid;
        var applied = Math.Min(note.Amount, Math.Max(0, openBalance));
        invoice.ApplyPayment(applied);
        note.Apply();

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(note);
    }

    public async Task<RefundDto> CreateRefundAsync(Guid tenantId, Guid paymentId, decimal amount, string? reason, CancellationToken cancellationToken = default)
    {
        var payment = await db.Payments.SingleOrDefaultAsync(p => p.TenantId == tenantId && p.Id == paymentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Payment), paymentId);

        if (amount > payment.Amount)
        {
            throw new DomainRuleException("REFUND_EXCEEDS_PAYMENT", $"Refund of {amount} exceeds payment {paymentId}'s amount of {payment.Amount}.");
        }

        var refund = new Refund(tenantId, paymentId, amount, reason);
        await db.Refunds.AddAsync(refund, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(refund);
    }

    public async Task<RefundDto> MarkRefundProcessedAsync(Guid tenantId, Guid refundId, string? gatewayRef, CancellationToken cancellationToken = default)
    {
        var refund = await db.Refunds.SingleOrDefaultAsync(r => r.TenantId == tenantId && r.Id == refundId, cancellationToken)
            ?? throw new NotFoundException(nameof(Refund), refundId);

        refund.MarkProcessed(gatewayRef);
        var payment = await db.Payments.SingleAsync(p => p.Id == refund.PaymentId, cancellationToken);
        payment.MarkRefunded();

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(refund);
    }

    public async Task<ClientStatementDto> GetStatementAsync(Guid tenantId, Guid clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var invoices = await db.Invoices.Where(i => i.TenantId == tenantId && i.ClientId == clientId && i.IssueDate >= from && i.IssueDate <= to).ToListAsync(cancellationToken);
        var payments = await db.Payments.Where(p => p.TenantId == tenantId && p.ClientId == clientId && p.ReceivedOn >= from && p.ReceivedOn <= to).ToListAsync(cancellationToken);

        var invoiceDtos = new List<InvoiceDto>();
        foreach (var invoice in invoices)
        {
            var lines = await db.InvoiceLines.Where(l => l.InvoiceId == invoice.Id).OrderBy(l => l.LineNo).ToListAsync(cancellationToken);
            var taxes = await db.InvoiceTaxes.Where(t => t.InvoiceId == invoice.Id).ToListAsync(cancellationToken);
            invoiceDtos.Add(new InvoiceDto(
                invoice.Id, invoice.Number, invoice.MatterId, invoice.ClientId, invoice.Status, invoice.IssueDate, invoice.DueDate,
                invoice.Currency, invoice.SubTotal, invoice.DiscountTotal, invoice.TaxTotal, invoice.GrandTotal, invoice.AmountPaid, invoice.Notes, invoice.PdfBlobPath,
                lines.Select(l => new InvoiceLineDto(l.Id, l.LineNo, l.Type, l.Description, l.Qty, l.Unit, l.Rate, l.Amount, l.TimeEntryIds)).ToList(),
                taxes.Select(t => new InvoiceTaxDto(t.Name, t.RatePct, t.TaxableAmount, t.Amount)).ToList()));
        }

        var paymentDtos = new List<PaymentDto>();
        foreach (var payment in payments)
        {
            paymentDtos.Add(await BuildDtoAsync(payment, cancellationToken));
        }

        // Opening/closing balance: a full historical ledger walk is out of scope for this pass
        // (documented simplification) — closing balance is the sum of open balances on every
        // invoice issued to date for this client, opening balance is that figure less this
        // period's net movement.
        var allInvoices = await db.Invoices.Where(i => i.TenantId == tenantId && i.ClientId == clientId && i.IssueDate <= to).ToListAsync(cancellationToken);
        var closingBalance = allInvoices.Sum(i => i.GrandTotal - i.AmountPaid);
        var periodNetMovement = invoiceDtos.Sum(i => i.GrandTotal) - paymentDtos.Sum(p => p.Amount);
        var openingBalance = closingBalance - periodNetMovement;

        return new ClientStatementDto(clientId, from, to, invoiceDtos, paymentDtos, openingBalance, closingBalance);
    }

    private async Task<string> GenerateReceiptNumberAsync(Guid tenantId, CancellationToken cancellationToken) => await GenerateSequencedNumberAsync(tenantId, "RCPT", cancellationToken);

    private async Task<string> GenerateSequencedNumberAsync(Guid tenantId, string seriesKey, CancellationToken cancellationToken)
    {
        var fiscalYear = DateTime.UtcNow.Month >= 4 ? DateTime.UtcNow.Year : DateTime.UtcNow.Year - 1;
        var series = await db.NumberSeries.SingleOrDefaultAsync(s => s.TenantId == tenantId && s.SeriesKey == seriesKey && s.BranchId == null && s.FiscalYear == fiscalYear, cancellationToken);
        if (series is null)
        {
            series = new Domain.Entities.NumberSeries(tenantId, seriesKey, fiscalYear, "{SERIES}-{FY}-{SEQ}", null);
            await db.NumberSeries.AddAsync(series, cancellationToken);
        }

        var seq = series.ConsumeNext();
        return $"{series.SeriesKey}-{fiscalYear}-{seq}";
    }

    private async Task<PaymentDto> BuildDtoAsync(Payment payment, CancellationToken cancellationToken)
    {
        var allocations = await db.PaymentAllocations.Where(a => a.PaymentId == payment.Id).ToListAsync(cancellationToken);
        return ToDto(payment, allocations);
    }

    private static PaymentDto ToDto(Payment payment, IReadOnlyList<PaymentAllocation> allocations) => new(
        payment.Id, payment.ReceiptNumber, payment.ClientId, payment.Amount, payment.Mode, payment.Gateway, payment.GatewayRef, payment.ReceivedOn, payment.Status,
        allocations.Select(a => new PaymentAllocationDto(a.InvoiceId, a.Amount)).ToList());

    private static CreditNoteDto ToDto(CreditNote note) => new(note.Id, note.Number, note.InvoiceId, note.Amount, note.Reason, note.Status);

    private static RefundDto ToDto(Refund refund) => new(refund.Id, refund.PaymentId, refund.Amount, refund.Reason, refund.Status, refund.GatewayRef);
}
