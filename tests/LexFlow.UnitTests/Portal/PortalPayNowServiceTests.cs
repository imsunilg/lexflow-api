using FluentAssertions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using LexFlow.Infrastructure.Portal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LexFlow.UnitTests.Portal;

/// <summary>
/// Module 17 "Pay Now" edge case: "invoice paid at bank same day as gateway (double-payment
/// guard: gateway session voided when invoice fully allocated; race -> auto-refund flow +
/// alert)." Also covers the plain happy path and the "no open balance" guard at session
/// creation.
/// </summary>
public sealed class PortalPayNowServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _clientId = Guid.NewGuid();

    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private async Task<Invoice> SeedInvoiceAsync(LexFlowDbContext db, decimal grandTotal, decimal amountPaid)
    {
        var matter = new Matter(_tenantId, "M-1", "Matter", _clientId, "Litigation", null, null, null, "Normal", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        var invoice = new Invoice(_tenantId, matter.Id, _clientId, null, null, "INR", null);
        invoice.SetTotals(grandTotal, 0, 0, grandTotal);
        invoice.MarkSent();
        if (amountPaid > 0)
        {
            invoice.ApplyPayment(amountPaid);
        }

        await db.Matters.AddAsync(matter);
        await db.Invoices.AddAsync(invoice);
        await db.SaveChangesAsync();
        return invoice;
    }

    [Fact]
    public async Task CreateSessionAsync_throws_when_the_invoice_has_no_open_balance()
    {
        await using var db = CreateContext(nameof(CreateSessionAsync_throws_when_the_invoice_has_no_open_balance));
        var invoice = await SeedInvoiceAsync(db, 100, 100);
        var service = new PortalPayNowService(db, [new FakeGateway("razorpay", captured: false)], new FakePaymentService(), NullLogger<PortalPayNowService>.Instance);

        var act = () => service.CreateSessionAsync(_tenantId, _clientId, null, invoice.Id, "https://portal.example/return");

        await act.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "INVOICE_ALREADY_PAID");
    }

    [Fact]
    public async Task CreateSessionAsync_voids_any_prior_open_session_for_the_same_invoice()
    {
        await using var db = CreateContext(nameof(CreateSessionAsync_voids_any_prior_open_session_for_the_same_invoice));
        var invoice = await SeedInvoiceAsync(db, 100, 0);
        var gateway = new FakeGateway("razorpay", captured: false);
        var service = new PortalPayNowService(db, [gateway], new FakePaymentService(), NullLogger<PortalPayNowService>.Instance);

        var first = await service.CreateSessionAsync(_tenantId, _clientId, null, invoice.Id, "https://portal.example/return");
        await service.CreateSessionAsync(_tenantId, _clientId, null, invoice.Id, "https://portal.example/return");

        var firstSession = await db.PaymentGatewaySessions.SingleAsync(s => s.Id == first.SessionId);
        firstSession.Status.Should().Be("Voided");
    }

    [Fact]
    public async Task ReconcileReturnAsync_captures_and_allocates_when_the_invoice_still_has_an_open_balance()
    {
        await using var db = CreateContext(nameof(ReconcileReturnAsync_captures_and_allocates_when_the_invoice_still_has_an_open_balance));
        var invoice = await SeedInvoiceAsync(db, 100, 0);
        var gateway = new FakeGateway("razorpay", captured: true, capturedAmount: 100);
        var paymentService = new FakePaymentService();
        var service = new PortalPayNowService(db, [gateway], paymentService, NullLogger<PortalPayNowService>.Instance);
        var session = await service.CreateSessionAsync(_tenantId, _clientId, null, invoice.Id, "https://portal.example/return");

        var result = await service.ReconcileReturnAsync(_tenantId, session.SessionId);

        result.Status.Should().Be("Captured");
        paymentService.CapturedCalls.Should().ContainSingle();
        var reloadedSession = await db.PaymentGatewaySessions.SingleAsync(s => s.Id == session.SessionId);
        reloadedSession.Status.Should().Be("Captured");
    }

    [Fact]
    public async Task ReconcileReturnAsync_applies_the_double_payment_guard_when_the_invoice_was_already_settled_before_capture()
    {
        await using var db = CreateContext(nameof(ReconcileReturnAsync_applies_the_double_payment_guard_when_the_invoice_was_already_settled_before_capture));
        var invoice = await SeedInvoiceAsync(db, 100, 0);
        var gateway = new FakeGateway("razorpay", captured: true, capturedAmount: 100);
        var paymentService = new FakePaymentService();
        var service = new PortalPayNowService(db, [gateway], paymentService, NullLogger<PortalPayNowService>.Instance);
        var session = await service.CreateSessionAsync(_tenantId, _clientId, null, invoice.Id, "https://portal.example/return");

        // Simulate a same-day bank payment settling the invoice in full before the gateway
        // return-URL reconciliation runs.
        invoice.ApplyPayment(100);
        await db.SaveChangesAsync();

        var result = await service.ReconcileReturnAsync(_tenantId, session.SessionId);

        result.Status.Should().Be("Voided");
        paymentService.RefundCalls.Should().ContainSingle();
        var reloadedSession = await db.PaymentGatewaySessions.SingleAsync(s => s.Id == session.SessionId);
        reloadedSession.Status.Should().Be("Voided");
        (await db.AuditEvents.Where(e => e.TenantId == _tenantId && e.Action == "paynow.double_payment_race").ToListAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task ReconcileReturnAsync_is_idempotent_for_an_already_captured_session()
    {
        await using var db = CreateContext(nameof(ReconcileReturnAsync_is_idempotent_for_an_already_captured_session));
        var invoice = await SeedInvoiceAsync(db, 100, 0);
        var gateway = new FakeGateway("razorpay", captured: true, capturedAmount: 100);
        var paymentService = new FakePaymentService();
        var service = new PortalPayNowService(db, [gateway], paymentService, NullLogger<PortalPayNowService>.Instance);
        var session = await service.CreateSessionAsync(_tenantId, _clientId, null, invoice.Id, "https://portal.example/return");
        await service.ReconcileReturnAsync(_tenantId, session.SessionId);

        await service.ReconcileReturnAsync(_tenantId, session.SessionId);

        paymentService.CapturedCalls.Should().ContainSingle();
    }

    private sealed class FakeGateway(string provider, bool captured, decimal? capturedAmount = null) : IPaymentGateway
    {
        public string Provider => provider;

        public Task<PaymentLinkResult> CreatePaymentLinkAsync(Guid tenantId, Guid invoiceId, decimal amount, string currency, string description, CancellationToken cancellationToken = default)
            => Task.FromResult(new PaymentLinkResult(true, "https://gateway.example/checkout", $"gw-{invoiceId}", null));

        public Task<GatewayPaymentStatusResult> GetPaymentStatusAsync(Guid tenantId, string gatewayRef, CancellationToken cancellationToken = default)
            => Task.FromResult(new GatewayPaymentStatusResult(captured, capturedAmount, null));
    }

    private sealed class FakePaymentService : IPaymentService
    {
        public List<(string Gateway, string GatewayRef, decimal Amount, Guid InvoiceId)> CapturedCalls { get; } = [];
        public List<(Guid PaymentId, decimal Amount)> RefundCalls { get; } = [];

        public Task<PaymentDto> RecordPaymentAsync(Guid tenantId, RecordPaymentInput input, CancellationToken cancellationToken = default)
        {
            var paymentId = Guid.NewGuid();
            return Task.FromResult(new PaymentDto(paymentId, "RCPT-1", input.ClientId, input.Amount, input.Mode, input.Gateway, input.GatewayRef, input.ReceivedOn, "Cleared", []));
        }

        public Task<PaymentDto> HandleGatewayCapturedAsync(Guid tenantId, string gateway, string gatewayRef, decimal amount, Guid invoiceId, CancellationToken cancellationToken = default)
        {
            CapturedCalls.Add((gateway, gatewayRef, amount, invoiceId));
            return Task.FromResult(new PaymentDto(Guid.NewGuid(), "RCPT-1", Guid.NewGuid(), amount, "Gateway", gateway, gatewayRef, DateOnly.FromDateTime(DateTime.UtcNow), "Cleared", []));
        }

        public Task<PaymentDto?> GetAsync(Guid tenantId, Guid paymentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<PaymentDto>> GetForClientAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CreditNoteDto> CreateCreditNoteAsync(Guid tenantId, Guid invoiceId, decimal amount, string reason, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CreditNoteDto> ApplyCreditNoteAsync(Guid tenantId, Guid creditNoteId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<RefundDto> CreateRefundAsync(Guid tenantId, Guid paymentId, decimal amount, string? reason, CancellationToken cancellationToken = default)
        {
            RefundCalls.Add((paymentId, amount));
            return Task.FromResult(new RefundDto(Guid.NewGuid(), paymentId, amount, reason, "Pending", null));
        }

        public Task<RefundDto> MarkRefundProcessedAsync(Guid tenantId, Guid refundId, string? gatewayRef, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ClientStatementDto> GetStatementAsync(Guid tenantId, Guid clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
