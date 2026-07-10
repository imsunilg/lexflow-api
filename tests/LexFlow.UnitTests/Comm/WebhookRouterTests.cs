using FluentAssertions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Comm;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Comm;

/// <summary>
/// §34: single-router signature verification + event-id dedupe. MSG91 doesn't sign
/// webhooks (documented in WebhookRouter itself), which makes it the one provider path
/// this suite can drive signature-valid without a live Key Vault secret — GatewayCredentialResolver
/// always resolves a null secret when no SecretClient is configured (same as every other
/// "conditional external" component in this codebase), so Twilio/WhatsApp HMAC verification
/// is exercised here only on its "no secret configured -> rejected" failure path.
/// </summary>
public sealed class WebhookRouterTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private static WebhookRouter CreateRouter(LexFlowDbContext db, FakeSmsService? sms = null, FakeWhatsAppService? whatsApp = null, FakeCallService? calls = null, FakeEmailService? email = null, FakePaymentService? payments = null)
        => new(db, new GatewayCredentialResolver(db), sms ?? new FakeSmsService(), whatsApp ?? new FakeWhatsAppService(), calls ?? new FakeCallService(), email ?? new FakeEmailService(), payments ?? new FakePaymentService());

    [Fact]
    public async Task HandleAsync_rejects_Stripe_when_no_gateway_secret_is_configured()
    {
        await using var db = CreateContext(nameof(HandleAsync_rejects_Stripe_when_no_gateway_secret_is_configured));
        var router = CreateRouter(db);
        var headers = new Dictionary<string, string> { ["Stripe-Signature"] = "t=1,v1=anything" };

        var result = await router.HandleAsync(Guid.NewGuid(), "payment_stripe", "{\"id\":\"evt_1\",\"type\":\"payment_intent.succeeded\"}", headers, CancellationToken.None);

        result.SignatureValid.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_processes_a_PayPal_capture_and_dedupes_a_redelivery()
    {
        await using var db = CreateContext(nameof(HandleAsync_processes_a_PayPal_capture_and_dedupes_a_redelivery));
        var tenantId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var payments = new FakePaymentService();
        var router = CreateRouter(db, payments: payments);
        var body = $"{{\"id\":\"WH-1\",\"event_type\":\"PAYMENT.CAPTURE.COMPLETED\",\"resource\":{{\"id\":\"5O1901\",\"custom_id\":\"{invoiceId}\",\"amount\":{{\"value\":\"250.00\"}}}}}}";

        var first = await router.HandleAsync(tenantId, "payment_paypal", body, new Dictionary<string, string>(), CancellationToken.None);
        var second = await router.HandleAsync(tenantId, "payment_paypal", body, new Dictionary<string, string>(), CancellationToken.None);

        first.Handled.Should().BeTrue();
        second.Duplicate.Should().BeTrue();
        payments.Captured.Should().ContainSingle(c => c.Gateway == "paypal" && c.GatewayRef == "5O1901" && c.Amount == 250.00m && c.InvoiceId == invoiceId);
    }

    [Fact]
    public async Task HandleAsync_rejects_an_unrecognized_provider()
    {
        await using var db = CreateContext(nameof(HandleAsync_rejects_an_unrecognized_provider));
        var router = CreateRouter(db);

        var result = await router.HandleAsync(Guid.NewGuid(), "unknown_provider", "{}", new Dictionary<string, string>(), CancellationToken.None);

        result.SignatureValid.Should().BeFalse();
        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_rejects_Twilio_when_no_gateway_secret_is_configured()
    {
        await using var db = CreateContext(nameof(HandleAsync_rejects_Twilio_when_no_gateway_secret_is_configured));
        var router = CreateRouter(db);
        var headers = new Dictionary<string, string> { ["X-Twilio-Signature"] = "anything", ["X-Webhook-Url"] = "https://api.test/webhooks/sms_twilio" };

        var result = await router.HandleAsync(Guid.NewGuid(), "sms_twilio", "MessageSid=SM1&MessageStatus=delivered", headers, CancellationToken.None);

        result.SignatureValid.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_processes_an_MSG91_status_update_and_dedupes_a_redelivery()
    {
        await using var db = CreateContext(nameof(HandleAsync_processes_an_MSG91_status_update_and_dedupes_a_redelivery));
        var tenantId = Guid.NewGuid();
        var sms = new FakeSmsService();
        var router = CreateRouter(db, sms: sms);
        var body = "MessageSid=SM123&MessageStatus=delivered";

        var first = await router.HandleAsync(tenantId, "sms_msg91", body, new Dictionary<string, string>(), CancellationToken.None);
        var second = await router.HandleAsync(tenantId, "sms_msg91", body, new Dictionary<string, string>(), CancellationToken.None);

        first.Handled.Should().BeTrue();
        first.Duplicate.Should().BeFalse();
        second.Duplicate.Should().BeTrue();
        second.Handled.Should().BeFalse();
        sms.StatusUpdates.Should().ContainSingle(u => u.ProviderMessageId == "SM123" && u.Status == "Delivered");
    }

    [Fact]
    public async Task HandleAsync_records_an_inbound_MSG91_SMS()
    {
        await using var db = CreateContext(nameof(HandleAsync_records_an_inbound_MSG91_SMS));
        var tenantId = Guid.NewGuid();
        var sms = new FakeSmsService();
        var router = CreateRouter(db, sms: sms);
        var body = "From=%2B919999999999&To=%2B910000000000&Body=Hello%20there&MessageSid=SM999";

        var result = await router.HandleAsync(tenantId, "sms_msg91", body, new Dictionary<string, string>(), CancellationToken.None);

        result.Handled.Should().BeTrue();
        sms.InboundRecorded.Should().ContainSingle(m => m.From == "+919999999999" && m.Body == "Hello there");
    }

    [Fact]
    public async Task HandleAsync_dedupes_by_tenant_so_two_tenants_can_process_the_same_event_id_independently()
    {
        await using var db = CreateContext(nameof(HandleAsync_dedupes_by_tenant_so_two_tenants_can_process_the_same_event_id_independently));
        var sms = new FakeSmsService();
        var router = CreateRouter(db, sms: sms);
        var body = "MessageSid=SM777&MessageStatus=sent";

        var tenantA = await router.HandleAsync(Guid.NewGuid(), "sms_msg91", body, new Dictionary<string, string>(), CancellationToken.None);
        var tenantB = await router.HandleAsync(Guid.NewGuid(), "sms_msg91", body, new Dictionary<string, string>(), CancellationToken.None);

        tenantA.Duplicate.Should().BeFalse();
        tenantB.Duplicate.Should().BeFalse("dedupe is scoped per tenant, not global");
    }

    private sealed class FakeSmsService : ISmsService
    {
        public List<(string ProviderMessageId, string Status)> StatusUpdates { get; } = [];
        public List<(string From, string To, string Body)> InboundRecorded { get; } = [];

        public Task<SmsMessageDto> SendAsync(Guid tenantId, SendSmsInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<SmsMessageDto>> GetForClientAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SmsMessageDto> RecordInboundAsync(Guid tenantId, string fromNumber, string toNumber, string body, string? providerMessageId, string provider, CancellationToken cancellationToken = default)
        {
            InboundRecorded.Add((fromNumber, toNumber, body));
            return Task.FromResult(new SmsMessageDto(Guid.NewGuid(), null, null, "Inbound", fromNumber, toNumber, body, null, provider, providerMessageId, "Delivered", DateTimeOffset.UtcNow));
        }

        public Task UpdateStatusAsync(Guid tenantId, string providerMessageId, string status, CancellationToken cancellationToken = default)
        {
            StatusUpdates.Add((providerMessageId, status));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWhatsAppService : IWhatsAppService
    {
        public Task<WhatsappMessageDto> SendAsync(Guid tenantId, SendWhatsAppInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<WhatsappMessageDto>> GetForClientAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> HasActiveOptInAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<WhatsAppOptinDto> OptInAsync(Guid tenantId, Guid clientId, string phoneE164, string? source, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task OptOutAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<WhatsappMessageDto> RecordInboundAsync(Guid tenantId, Guid? clientId, string waMsgId, string? body, CancellationToken cancellationToken = default)
            => Task.FromResult(new WhatsappMessageDto(Guid.NewGuid(), clientId, waMsgId, "Inbound", null, body, "Delivered", DateTimeOffset.UtcNow.AddHours(24)));

        public Task UpdateStatusAsync(Guid tenantId, string waMsgId, string status, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeCallService : ICallService
    {
        public Task<CallLogDto> LogAsync(Guid tenantId, Guid? actorId, LogCallInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<CallLogDto>> GetForClientAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<CallLogDto> ClickToCallAsync(Guid tenantId, Guid actorId, ClickToCallInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task RecordCallStatusAsync(Guid tenantId, string providerCallId, int durationSec, string? recordingBlobPath, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeEmailService : IEmailService
    {
        public Task<EmailThreadDto> SendAsync(Guid tenantId, Guid? actorId, Guid mailboxId, EmailSendRequest request, Guid? matterId, Guid? clientId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<EmailThreadDto>> GetThreadsAsync(Guid tenantId, Guid? matterId, Guid? clientId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<EmailThreadDto>> GetTriageQueueAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<EmailMessageDto>> GetMessagesAsync(Guid tenantId, Guid threadId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<EmailThreadDto> LinkThreadToMatterAsync(Guid tenantId, Guid threadId, Guid matterId, Guid? clientId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<EmailThreadDto> HandleInboundAsync(Guid tenantId, InboundEmailInput input, CancellationToken cancellationToken = default)
            => Task.FromResult(new EmailThreadDto(Guid.NewGuid(), input.Subject, null, null, null, input.SentAt));
    }

    private sealed class FakePaymentService : IPaymentService
    {
        public List<(string Gateway, string GatewayRef, decimal Amount, Guid InvoiceId)> Captured { get; } = [];

        public Task<PaymentDto> RecordPaymentAsync(Guid tenantId, RecordPaymentInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<PaymentDto> HandleGatewayCapturedAsync(Guid tenantId, string gateway, string gatewayRef, decimal amount, Guid invoiceId, CancellationToken cancellationToken = default)
        {
            Captured.Add((gateway, gatewayRef, amount, invoiceId));
            return Task.FromResult(new PaymentDto(Guid.NewGuid(), "RCPT-1", Guid.NewGuid(), amount, "Gateway", gateway, gatewayRef, DateOnly.FromDateTime(DateTime.UtcNow), "Cleared", []));
        }

        public Task<PaymentDto?> GetAsync(Guid tenantId, Guid paymentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<PaymentDto>> GetForClientAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<CreditNoteDto> CreateCreditNoteAsync(Guid tenantId, Guid invoiceId, decimal amount, string reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<CreditNoteDto> ApplyCreditNoteAsync(Guid tenantId, Guid creditNoteId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<RefundDto> CreateRefundAsync(Guid tenantId, Guid paymentId, decimal amount, string? reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<RefundDto> MarkRefundProcessedAsync(Guid tenantId, Guid refundId, string? gatewayRef, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ClientStatementDto> GetStatementAsync(Guid tenantId, Guid clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
