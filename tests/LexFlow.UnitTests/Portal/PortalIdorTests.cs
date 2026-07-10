using FluentAssertions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using LexFlow.Infrastructure.Portal;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Portal;

/// <summary>
/// AC-P1: "Portal user A can never fetch any resource of client B (IDOR test suite passes
/// 100%)." Every portal service method that takes a resource id is exercised here with a
/// caller scoped to Client A attempting to reach a same-tenant resource that actually belongs
/// to Client B — every one must throw NotFoundException (never leak existence via 403, per
/// this codebase's enumeration-safe convention), and every list endpoint must return only the
/// caller's own data even when both clients have rows.
/// </summary>
public sealed class PortalIdorTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _clientA = Guid.NewGuid();
    private readonly Guid _clientB = Guid.NewGuid();

    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private async Task<(Matter MatterA, Matter MatterB)> SeedMattersAsync(LexFlowDbContext db)
    {
        var matterA = new Matter(_tenantId, "M-A", "Matter A", _clientA, "Litigation", null, null, null, "Normal", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        var matterB = new Matter(_tenantId, "M-B", "Matter B", _clientB, "Litigation", null, null, null, "Normal", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        await db.Matters.AddRangeAsync(matterA, matterB);
        await db.SaveChangesAsync();
        return (matterA, matterB);
    }

    [Fact]
    public async Task Timeline_GetMyMattersAsync_never_returns_another_clients_matters()
    {
        await using var db = CreateContext(nameof(Timeline_GetMyMattersAsync_never_returns_another_clients_matters));
        await SeedMattersAsync(db);
        var service = new PortalTimelineService(db);

        var result = await service.GetMyMattersAsync(_tenantId, _clientA, visibleMatterIds: null);

        result.Should().ContainSingle();
        result[0].Title.Should().Be("Matter A");
    }

    [Fact]
    public async Task Timeline_GetTimelineAsync_throws_NotFound_for_another_clients_matter()
    {
        await using var db = CreateContext(nameof(Timeline_GetTimelineAsync_throws_NotFound_for_another_clients_matter));
        var (_, matterB) = await SeedMattersAsync(db);
        var service = new PortalTimelineService(db);

        var act = () => service.GetTimelineAsync(_tenantId, _clientA, visibleMatterIds: null, matterB.Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Timeline_GetTimelineAsync_throws_NotFound_when_matter_is_outside_the_callers_visible_subset()
    {
        await using var db = CreateContext(nameof(Timeline_GetTimelineAsync_throws_NotFound_when_matter_is_outside_the_callers_visible_subset));
        var (matterA, _) = await SeedMattersAsync(db);
        var otherMatterA = new Matter(_tenantId, "M-A2", "Matter A2", _clientA, "Litigation", null, null, null, "Normal", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        await db.Matters.AddAsync(otherMatterA);
        await db.SaveChangesAsync();
        var service = new PortalTimelineService(db);

        // Corporate per-user scoping: this portal user may only see matterA, not otherMatterA, even though both belong to the same client.
        var act = () => service.GetTimelineAsync(_tenantId, _clientA, visibleMatterIds: [matterA.Id], otherMatterA.Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Invoices_GetInvoicesAsync_never_returns_another_clients_invoices()
    {
        await using var db = CreateContext(nameof(Invoices_GetInvoicesAsync_never_returns_another_clients_invoices));
        var (matterA, matterB) = await SeedMattersAsync(db);
        var invoiceA = new Invoice(_tenantId, matterA.Id, _clientA, null, null, "INR", null);
        invoiceA.SetTotals(100, 0, 0, 100);
        invoiceA.MarkSent();
        var invoiceB = new Invoice(_tenantId, matterB.Id, _clientB, null, null, "INR", null);
        invoiceB.SetTotals(200, 0, 0, 200);
        invoiceB.MarkSent();
        await db.Invoices.AddRangeAsync(invoiceA, invoiceB);
        await db.SaveChangesAsync();
        var service = new PortalInvoiceService(db);

        var result = await service.GetInvoicesAsync(_tenantId, _clientA);

        result.Should().ContainSingle();
        result[0].GrandTotal.Should().Be(100);
    }

    [Fact]
    public async Task Invoices_GetInvoiceAsync_throws_NotFound_for_another_clients_invoice()
    {
        await using var db = CreateContext(nameof(Invoices_GetInvoiceAsync_throws_NotFound_for_another_clients_invoice));
        var (_, matterB) = await SeedMattersAsync(db);
        var invoiceB = new Invoice(_tenantId, matterB.Id, _clientB, null, null, "INR", null);
        invoiceB.SetTotals(200, 0, 0, 200);
        invoiceB.MarkSent();
        await db.Invoices.AddAsync(invoiceB);
        await db.SaveChangesAsync();
        var service = new PortalInvoiceService(db);

        var act = () => service.GetInvoiceAsync(_tenantId, _clientA, invoiceB.Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task PayNow_CreateSessionAsync_throws_NotFound_for_another_clients_invoice()
    {
        await using var db = CreateContext(nameof(PayNow_CreateSessionAsync_throws_NotFound_for_another_clients_invoice));
        var (_, matterB) = await SeedMattersAsync(db);
        var invoiceB = new Invoice(_tenantId, matterB.Id, _clientB, null, null, "INR", null);
        invoiceB.SetTotals(200, 0, 0, 200);
        invoiceB.MarkSent();
        await db.Invoices.AddAsync(invoiceB);
        await db.SaveChangesAsync();
        var service = new PortalPayNowService(db, [], new FakePaymentService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<PortalPayNowService>.Instance);

        var act = () => service.CreateSessionAsync(_tenantId, _clientA, null, invoiceB.Id, "https://portal.example/return");

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task PayNow_ReconcileReturnAsync_throws_NotFound_for_a_session_in_a_different_tenant()
    {
        await using var db = CreateContext(nameof(PayNow_ReconcileReturnAsync_throws_NotFound_for_a_session_in_a_different_tenant));
        var (_, matterB) = await SeedMattersAsync(db);
        var invoiceB = new Invoice(_tenantId, matterB.Id, _clientB, null, null, "INR", null);
        invoiceB.SetTotals(200, 0, 0, 200);
        invoiceB.MarkSent();
        await db.Invoices.AddAsync(invoiceB);
        var session = new PaymentGatewaySession(_tenantId, invoiceB.Id, null, "razorpay", 200, "https://portal.example/return");
        await db.PaymentGatewaySessions.AddAsync(session);
        await db.SaveChangesAsync();
        var service = new PortalPayNowService(db, [], new FakePaymentService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<PortalPayNowService>.Instance);

        var act = () => service.ReconcileReturnAsync(Guid.NewGuid(), session.Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Documents_GetDocumentsAsync_never_returns_another_clients_documents()
    {
        await using var db = CreateContext(nameof(Documents_GetDocumentsAsync_never_returns_another_clients_documents));
        var (matterA, matterB) = await SeedMattersAsync(db);
        var docA = new Document(_tenantId, null, matterA.Id, _clientA, null, "Doc A", "Contract", "Normal");
        docA.SetPortalPublished(true);
        var docB = new Document(_tenantId, null, matterB.Id, _clientB, null, "Doc B", "Contract", "Normal");
        docB.SetPortalPublished(true);
        await db.Documents.AddRangeAsync(docA, docB);
        await db.SaveChangesAsync();
        var service = new PortalDocumentService(db, new FakeDocumentService(), new FakeNotificationService());

        var result = await service.GetDocumentsAsync(_tenantId, _clientA, visibleMatterIds: null, matterId: null);

        result.Should().ContainSingle();
        result[0].Title.Should().Be("Doc A");
    }

    [Fact]
    public async Task Documents_GetDownloadUrlAsync_throws_NotFound_for_another_clients_document()
    {
        await using var db = CreateContext(nameof(Documents_GetDownloadUrlAsync_throws_NotFound_for_another_clients_document));
        var (_, matterB) = await SeedMattersAsync(db);
        var docB = new Document(_tenantId, null, matterB.Id, _clientB, null, "Doc B", "Contract", "Normal");
        docB.SetPortalPublished(true);
        await db.Documents.AddAsync(docB);
        await db.SaveChangesAsync();
        var service = new PortalDocumentService(db, new FakeDocumentService(), new FakeNotificationService());

        var act = () => service.GetDownloadUrlAsync(_tenantId, _clientA, visibleMatterIds: null, docB.Id, null, null);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Documents_GetDownloadUrlAsync_throws_NotFound_for_an_unpublished_document_even_if_owned_by_the_caller()
    {
        await using var db = CreateContext(nameof(Documents_GetDownloadUrlAsync_throws_NotFound_for_an_unpublished_document_even_if_owned_by_the_caller));
        var (matterA, _) = await SeedMattersAsync(db);
        var unpublished = new Document(_tenantId, null, matterA.Id, _clientA, null, "Draft", "Contract", "Normal");
        await db.Documents.AddAsync(unpublished);
        await db.SaveChangesAsync();
        var service = new PortalDocumentService(db, new FakeDocumentService(), new FakeNotificationService());

        var act = () => service.GetDownloadUrlAsync(_tenantId, _clientA, visibleMatterIds: null, unpublished.Id, null, null);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Documents_UploadAsync_throws_NotFound_for_another_clients_matter()
    {
        await using var db = CreateContext(nameof(Documents_UploadAsync_throws_NotFound_for_another_clients_matter));
        var (_, matterB) = await SeedMattersAsync(db);
        var service = new PortalDocumentService(db, new FakeDocumentService(), new FakeNotificationService());

        var act = () => service.UploadAsync(_tenantId, Guid.NewGuid(), _clientA, visibleMatterIds: null, matterB.Id, [1, 2, 3], "f.pdf", "application/pdf", "Title");

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Appointments_GetAppointmentsAsync_never_returns_another_clients_appointments()
    {
        await using var db = CreateContext(nameof(Appointments_GetAppointmentsAsync_never_returns_another_clients_appointments));
        var (matterA, matterB) = await SeedMattersAsync(db);
        var portalUserA = new ClientPortalUser(_tenantId, _clientA, "a@example.com", "A");
        var portalUserB = new ClientPortalUser(_tenantId, _clientB, "b@example.com", "B");
        await db.ClientPortalUsers.AddRangeAsync(portalUserA, portalUserB);
        var lawyerId = Guid.NewGuid();
        var apptA = new PortalAppointmentRequest(_tenantId, portalUserA.Id, matterA.Id, lawyerId, DateTimeOffset.UtcNow.AddDays(3), DateTimeOffset.UtcNow.AddDays(3).AddHours(1), null);
        var apptB = new PortalAppointmentRequest(_tenantId, portalUserB.Id, matterB.Id, lawyerId, DateTimeOffset.UtcNow.AddDays(3), DateTimeOffset.UtcNow.AddDays(3).AddHours(1), null);
        await db.PortalAppointmentRequests.AddRangeAsync(apptA, apptB);
        await db.SaveChangesAsync();
        var service = new PortalAppointmentService(db);

        var result = await service.GetAppointmentsAsync(_tenantId, _clientA);

        result.Should().ContainSingle();
        result[0].MatterId.Should().Be(matterA.Id);
    }

    [Fact]
    public async Task Appointments_RequestAppointmentAsync_throws_NotFound_for_another_clients_matter()
    {
        await using var db = CreateContext(nameof(Appointments_RequestAppointmentAsync_throws_NotFound_for_another_clients_matter));
        var (_, matterB) = await SeedMattersAsync(db);
        var service = new PortalAppointmentService(db);

        var act = () => service.RequestAppointmentAsync(
            _tenantId, Guid.NewGuid(), _clientA, visibleMatterIds: null, matterB.Id, Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(3), DateTimeOffset.UtcNow.AddDays(3).AddHours(1), null);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Appointments_RequestAppointmentAsync_throws_DomainRuleException_when_less_than_24h_ahead()
    {
        await using var db = CreateContext(nameof(Appointments_RequestAppointmentAsync_throws_DomainRuleException_when_less_than_24h_ahead));
        var (matterA, _) = await SeedMattersAsync(db);
        var service = new PortalAppointmentService(db);

        var act = () => service.RequestAppointmentAsync(
            _tenantId, Guid.NewGuid(), _clientA, visibleMatterIds: null, matterA.Id, Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow.AddHours(2), null);

        await act.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "APPOINTMENT_TOO_SOON");
    }

    [Fact]
    public async Task Messages_GetThreadsAsync_never_returns_another_clients_threads()
    {
        await using var db = CreateContext(nameof(Messages_GetThreadsAsync_never_returns_another_clients_threads));
        var (matterA, matterB) = await SeedMattersAsync(db);
        var threadA = new PortalMessageThread(_tenantId, matterA.Id, "Thread A");
        var threadB = new PortalMessageThread(_tenantId, matterB.Id, "Thread B");
        await db.PortalMessageThreads.AddRangeAsync(threadA, threadB);
        await db.SaveChangesAsync();
        var service = new PortalMessagingService(db);

        var result = await service.GetThreadsAsync(_tenantId, _clientA, visibleMatterIds: null);

        result.Should().ContainSingle();
        result[0].Subject.Should().Be("Thread A");
    }

    [Fact]
    public async Task Messages_GetMessagesAsync_throws_NotFound_for_another_clients_thread()
    {
        await using var db = CreateContext(nameof(Messages_GetMessagesAsync_throws_NotFound_for_another_clients_thread));
        var (_, matterB) = await SeedMattersAsync(db);
        var threadB = new PortalMessageThread(_tenantId, matterB.Id, "Thread B");
        await db.PortalMessageThreads.AddAsync(threadB);
        await db.SaveChangesAsync();
        var service = new PortalMessagingService(db);

        var act = () => service.GetMessagesAsync(_tenantId, _clientA, visibleMatterIds: null, threadB.Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Messages_PostMessageAsync_throws_NotFound_for_another_clients_thread()
    {
        await using var db = CreateContext(nameof(Messages_PostMessageAsync_throws_NotFound_for_another_clients_thread));
        var (_, matterB) = await SeedMattersAsync(db);
        var threadB = new PortalMessageThread(_tenantId, matterB.Id, "Thread B");
        await db.PortalMessageThreads.AddAsync(threadB);
        await db.SaveChangesAsync();
        var service = new PortalMessagingService(db);

        var act = () => service.PostMessageAsync(_tenantId, Guid.NewGuid(), _clientA, visibleMatterIds: null, threadB.Id, "Hello");

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Scope_GetVisibleMatterIdsAsync_returns_null_for_an_individual_client_and_the_stored_subset_for_a_corporate_client()
    {
        await using var db = CreateContext(nameof(Scope_GetVisibleMatterIdsAsync_returns_null_for_an_individual_client_and_the_stored_subset_for_a_corporate_client));
        var individual = new ClientPortalUser(_tenantId, _clientA, "solo@example.com", "Solo");
        var corporate = new ClientPortalUser(_tenantId, _clientB, "corp@example.com", "Corp");
        var restrictedMatterId = Guid.NewGuid();
        corporate.SetVisibleMatterIds([restrictedMatterId]);
        await db.ClientPortalUsers.AddRangeAsync(individual, corporate);
        await db.SaveChangesAsync();
        var service = new PortalScopeService(db);

        (await service.GetVisibleMatterIdsAsync(_tenantId, individual.Id)).Should().BeNull();
        (await service.GetVisibleMatterIdsAsync(_tenantId, corporate.Id)).Should().Equal(restrictedMatterId);
    }

    private sealed class FakePaymentService : IPaymentService
    {
        public Task<PaymentDto> RecordPaymentAsync(Guid tenantId, RecordPaymentInput input, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PaymentDto> HandleGatewayCapturedAsync(Guid tenantId, string gateway, string gatewayRef, decimal amount, Guid invoiceId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PaymentDto?> GetAsync(Guid tenantId, Guid paymentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<PaymentDto>> GetForClientAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CreditNoteDto> CreateCreditNoteAsync(Guid tenantId, Guid invoiceId, decimal amount, string reason, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CreditNoteDto> ApplyCreditNoteAsync(Guid tenantId, Guid creditNoteId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RefundDto> CreateRefundAsync(Guid tenantId, Guid paymentId, decimal amount, string? reason, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RefundDto> MarkRefundProcessedAsync(Guid tenantId, Guid refundId, string? gatewayRef, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ClientStatementDto> GetStatementAsync(Guid tenantId, Guid clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FakeDocumentService : IDocumentService
    {
        public Task<DocumentDto> CreateAsync(Guid tenantId, Guid? actorId, CreateDocumentInput input, byte[] fileContent, string fileName, string? mime, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DocumentDto?> GetByIdAsync(Guid tenantId, Guid documentId, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<DocumentDto>> GetAllAsync(Guid tenantId, DocumentFilter filter, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DocumentDto> UpdateMetadataAsync(Guid tenantId, Guid documentId, string title, string docType, string confidentiality, Guid? folderId, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteAsync(Guid tenantId, Guid documentId, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DocumentVersionDto> AddVersionAsync(Guid tenantId, Guid? actorId, Guid documentId, byte[] fileContent, string fileName, string? mime, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<DocumentVersionDto>> GetVersionsAsync(Guid tenantId, Guid documentId, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DocumentVersionDto> RestoreVersionAsync(Guid tenantId, Guid? actorId, Guid documentId, int versionNo, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<string> GetDownloadUrlAsync(Guid tenantId, Guid? actorId, Guid documentId, string? ip, string? userAgent, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default) => Task.FromResult("https://blob.example/doc");
        public Task LogActivityAsync(Guid tenantId, Guid? actorId, Guid documentId, string action, string? ip, string? userAgent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task BulkActionAsync(Guid tenantId, Guid? actorId, string action, IReadOnlyList<Guid> documentIds, Guid? targetFolderId, IReadOnlyList<string>? tagNames, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public Task<NotificationDispatchResult> NotifyAsync(Guid tenantId, Guid userId, NotifyRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new NotificationDispatchResult(Guid.NewGuid(), [], []));
        public Task<IReadOnlyList<NotificationDto>> GetForUserAsync(Guid tenantId, Guid userId, bool unreadOnly, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task MarkReadAsync(Guid tenantId, Guid userId, Guid notificationId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
