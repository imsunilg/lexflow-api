using FluentAssertions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Fin;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Fin;

/// <summary>Module 8 payments: idempotent recording (AC-B3), over-allocation guards (§19.8), gateway-captured ingestion, credit notes, and refunds.</summary>
public sealed class PaymentServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private static async Task<Invoice> SeedInvoiceAsync(LexFlowDbContext db, Guid tenantId, decimal grandTotal)
    {
        var invoice = new Invoice(tenantId, Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow).AddDays(15), "INR", null);
        invoice.SetTotals(grandTotal, 0, 0, grandTotal);
        invoice.MarkSent();
        await db.Invoices.AddAsync(invoice);
        await db.SaveChangesAsync();
        return invoice;
    }

    [Fact]
    public async Task RecordPaymentAsync_allocates_to_the_invoice_and_marks_it_paid()
    {
        await using var db = CreateContext(nameof(RecordPaymentAsync_allocates_to_the_invoice_and_marks_it_paid));
        var service = new PaymentService(db);
        var tenantId = Guid.NewGuid();
        var invoice = await SeedInvoiceAsync(db, tenantId, 10000);

        var payment = await service.RecordPaymentAsync(tenantId, new RecordPaymentInput(invoice.ClientId, 10000, "NEFT", null, null, DateOnly.FromDateTime(DateTime.UtcNow), [new PaymentAllocationInput(invoice.Id, 10000)], null), CancellationToken.None);

        payment.Allocations.Should().ContainSingle(a => a.InvoiceId == invoice.Id && a.Amount == 10000);
        (await db.Invoices.SingleAsync(i => i.Id == invoice.Id)).Status.Should().Be("Paid");
    }

    [Fact]
    public async Task RecordPaymentAsync_is_idempotent_by_key_and_makes_no_double_entry()
    {
        await using var db = CreateContext(nameof(RecordPaymentAsync_is_idempotent_by_key_and_makes_no_double_entry));
        var service = new PaymentService(db);
        var tenantId = Guid.NewGuid();
        var invoice = await SeedInvoiceAsync(db, tenantId, 5000);
        var input = new RecordPaymentInput(invoice.ClientId, 5000, "UPI", "razorpay", "pay_123", DateOnly.FromDateTime(DateTime.UtcNow), [new PaymentAllocationInput(invoice.Id, 5000)], "idem-key-1");

        var first = await service.RecordPaymentAsync(tenantId, input, CancellationToken.None);
        var second = await service.RecordPaymentAsync(tenantId, input, CancellationToken.None);

        second.Id.Should().Be(first.Id);
        (await db.Payments.CountAsync(p => p.TenantId == tenantId)).Should().Be(1);
    }

    [Fact]
    public async Task RecordPaymentAsync_throws_when_allocations_exceed_the_payment_amount()
    {
        await using var db = CreateContext(nameof(RecordPaymentAsync_throws_when_allocations_exceed_the_payment_amount));
        var service = new PaymentService(db);
        var tenantId = Guid.NewGuid();
        var invoice = await SeedInvoiceAsync(db, tenantId, 10000);

        var act = () => service.RecordPaymentAsync(tenantId, new RecordPaymentInput(invoice.ClientId, 1000, "Cash", null, null, DateOnly.FromDateTime(DateTime.UtcNow), [new PaymentAllocationInput(invoice.Id, 5000)], null), CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "OVER_ALLOCATION");
    }

    [Fact]
    public async Task RecordPaymentAsync_throws_when_an_allocation_exceeds_the_invoices_open_balance()
    {
        await using var db = CreateContext(nameof(RecordPaymentAsync_throws_when_an_allocation_exceeds_the_invoices_open_balance));
        var service = new PaymentService(db);
        var tenantId = Guid.NewGuid();
        var invoice = await SeedInvoiceAsync(db, tenantId, 1000);

        var act = () => service.RecordPaymentAsync(tenantId, new RecordPaymentInput(invoice.ClientId, 5000, "Cash", null, null, DateOnly.FromDateTime(DateTime.UtcNow), [new PaymentAllocationInput(invoice.Id, 5000)], null), CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "OVER_ALLOCATION");
    }

    [Fact]
    public async Task HandleGatewayCapturedAsync_is_exactly_once_for_the_same_gateway_ref()
    {
        await using var db = CreateContext(nameof(HandleGatewayCapturedAsync_is_exactly_once_for_the_same_gateway_ref));
        var service = new PaymentService(db);
        var tenantId = Guid.NewGuid();
        var invoice = await SeedInvoiceAsync(db, tenantId, 20000);

        await service.HandleGatewayCapturedAsync(tenantId, "razorpay", "pay_abc", 20000, invoice.Id, CancellationToken.None);
        await service.HandleGatewayCapturedAsync(tenantId, "razorpay", "pay_abc", 20000, invoice.Id, CancellationToken.None);

        (await db.Payments.CountAsync(p => p.TenantId == tenantId)).Should().Be(1);
        (await db.Invoices.SingleAsync(i => i.Id == invoice.Id)).AmountPaid.Should().Be(20000);
    }

    [Fact]
    public async Task ApplyCreditNoteAsync_reduces_the_invoices_open_balance_and_never_exceeds_it()
    {
        await using var db = CreateContext(nameof(ApplyCreditNoteAsync_reduces_the_invoices_open_balance_and_never_exceeds_it));
        var service = new PaymentService(db);
        var tenantId = Guid.NewGuid();
        var invoice = await SeedInvoiceAsync(db, tenantId, 3000);

        var note = await service.CreateCreditNoteAsync(tenantId, invoice.Id, 10000, "over-billed", CancellationToken.None);
        var applied = await service.ApplyCreditNoteAsync(tenantId, note.Id, CancellationToken.None);

        applied.Status.Should().Be("Applied");
        (await db.Invoices.SingleAsync(i => i.Id == invoice.Id)).AmountPaid.Should().Be(3000); // capped at the open balance, not the full 10000 credit
    }

    [Fact]
    public async Task CreateRefundAsync_throws_when_the_refund_exceeds_the_original_payment()
    {
        await using var db = CreateContext(nameof(CreateRefundAsync_throws_when_the_refund_exceeds_the_original_payment));
        var service = new PaymentService(db);
        var tenantId = Guid.NewGuid();
        var invoice = await SeedInvoiceAsync(db, tenantId, 5000);
        var payment = await service.RecordPaymentAsync(tenantId, new RecordPaymentInput(invoice.ClientId, 5000, "Cash", null, null, DateOnly.FromDateTime(DateTime.UtcNow), [new PaymentAllocationInput(invoice.Id, 5000)], null), CancellationToken.None);

        var act = () => service.CreateRefundAsync(tenantId, payment.Id, 6000, "overpaid", CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "REFUND_EXCEEDS_PAYMENT");
    }
}
