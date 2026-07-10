using FluentAssertions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Fin;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Fin;

/// <summary>Module 8: invoice draft creation (WIP pull), BR-9 GST tax computation, and the approval/send/void workflow. AC-B1/AC-B2/AC-B5/AC-B6.</summary>
public sealed class BillingServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private static BillingService CreateService(LexFlowDbContext db) => new(db, new InvoicePdfRenderer(), new FakeBlobStorageService());

    private static async Task<(Matter Matter, Client Client)> SeedMatterAndClientAsync(LexFlowDbContext db, Guid tenantId, Guid? branchId, string? clientGstin)
    {
        var client = new Client(tenantId, "CL-1", "Individual", "Kavita", "Rao", null, "k@x.com", "+919999999999", clientGstin, null, null, null, null);
        var matter = new Matter(tenantId, "MAT-1", "Rao v. Rao", client.Id, "Litigation", null, branchId, null, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        await db.Clients.AddAsync(client);
        await db.Matters.AddAsync(matter);
        await db.SaveChangesAsync();
        return (matter, client);
    }

    private static async Task<TimeEntry> SeedApprovedTimeEntryAsync(LexFlowDbContext db, Guid tenantId, Guid matterId, decimal amount)
    {
        var entry = new TimeEntry(tenantId, Guid.NewGuid(), matterId, null, DateOnly.FromDateTime(DateTime.UtcNow), null, 60, 60, true, "Court appearance", null, "manual");
        entry.Submit();
        entry.Approve(Guid.NewGuid(), amount, amount);
        await db.TimeEntries.AddAsync(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    [Fact]
    public async Task CreateDraftAsync_pulls_approved_time_entries_as_lines_and_sums_to_the_subtotal()
    {
        await using var db = CreateContext(nameof(CreateDraftAsync_pulls_approved_time_entries_as_lines_and_sums_to_the_subtotal));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();
        var (matter, _) = await SeedMatterAndClientAsync(db, tenantId, null, null);
        var entry1 = await SeedApprovedTimeEntryAsync(db, tenantId, matter.Id, 6000);
        var entry2 = await SeedApprovedTimeEntryAsync(db, tenantId, matter.Id, 4000);

        var invoice = await service.CreateDraftAsync(tenantId, matter.Id, new CreateInvoiceInput(null, 15, [entry1.Id, entry2.Id], null, null, null), CancellationToken.None);

        invoice.Status.Should().Be("Draft");
        invoice.SubTotal.Should().Be(10000);
        invoice.Lines.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateDraftAsync_throws_when_pulling_a_time_entry_that_is_not_approved()
    {
        await using var db = CreateContext(nameof(CreateDraftAsync_throws_when_pulling_a_time_entry_that_is_not_approved));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();
        var (matter, _) = await SeedMatterAndClientAsync(db, tenantId, null, null);
        var draftEntry = new TimeEntry(tenantId, Guid.NewGuid(), matter.Id, null, DateOnly.FromDateTime(DateTime.UtcNow), null, 30, 30, true, "Draft narrative", null, "manual");
        await db.TimeEntries.AddAsync(draftEntry);
        await db.SaveChangesAsync();

        var act = () => service.CreateDraftAsync(tenantId, matter.Id, new CreateInvoiceInput(null, 15, [draftEntry.Id], null, null, null), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().Where(e => e.Code == "TIME_ENTRY_NOT_BILLABLE_WIP");
    }

    [Fact]
    public async Task SendAsync_throws_TAX_NOT_CONFIGURED_when_no_tax_config_exists()
    {
        await using var db = CreateContext(nameof(SendAsync_throws_TAX_NOT_CONFIGURED_when_no_tax_config_exists));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();
        var (matter, _) = await SeedMatterAndClientAsync(db, tenantId, null, null);
        var entry = await SeedApprovedTimeEntryAsync(db, tenantId, matter.Id, 5000);
        var invoice = await service.CreateDraftAsync(tenantId, matter.Id, new CreateInvoiceInput(null, 15, [entry.Id], null, null, null), CancellationToken.None);

        var act = () => service.SendAsync(tenantId, Guid.NewGuid(), invoice.Id, CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "TAX_NOT_CONFIGURED");
    }

    [Fact]
    public async Task SendAsync_computes_CGST_and_SGST_for_an_intra_state_client_and_assigns_a_number()
    {
        await using var db = CreateContext(nameof(SendAsync_computes_CGST_and_SGST_for_an_intra_state_client_and_assigns_a_number));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();
        var branch = new Branch(tenantId, "Mumbai", "MUM");
        branch.Update("Mumbai", "MUM", "{}", "Asia/Kolkata", "27AAAAA0000A1Z5", "MUM");
        await db.Branches.AddAsync(branch);
        await db.TaxConfigs.AddAsync(new TaxConfig(tenantId, "IN", "GST", "{\"cgstPct\":9,\"sgstPct\":9,\"igstPct\":18}"));
        await db.SaveChangesAsync();

        var (matter, _) = await SeedMatterAndClientAsync(db, tenantId, branch.Id, "27BBBBB1111B1Z1"); // same state code "27" -> intra-state
        var entry = await SeedApprovedTimeEntryAsync(db, tenantId, matter.Id, 10000);
        var draft = await service.CreateDraftAsync(tenantId, matter.Id, new CreateInvoiceInput(null, 15, [entry.Id], null, null, null), CancellationToken.None);

        var sent = await service.SendAsync(tenantId, Guid.NewGuid(), draft.Id, CancellationToken.None);

        sent.Status.Should().Be("Sent");
        sent.Number.Should().NotBeNullOrEmpty();
        sent.Taxes.Should().Contain(t => t.Name == "CGST" && t.Amount == 900);
        sent.Taxes.Should().Contain(t => t.Name == "SGST" && t.Amount == 900);
        sent.GrandTotal.Should().Be(11800);
    }

    [Fact]
    public async Task SendAsync_computes_IGST_for_an_inter_state_client()
    {
        await using var db = CreateContext(nameof(SendAsync_computes_IGST_for_an_inter_state_client));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();
        var branch = new Branch(tenantId, "Mumbai", "MUM");
        branch.Update("Mumbai", "MUM", "{}", "Asia/Kolkata", "27AAAAA0000A1Z5", "MUM");
        await db.Branches.AddAsync(branch);
        await db.TaxConfigs.AddAsync(new TaxConfig(tenantId, "IN", "GST", "{\"cgstPct\":9,\"sgstPct\":9,\"igstPct\":18}"));
        await db.SaveChangesAsync();

        var (matter, _) = await SeedMatterAndClientAsync(db, tenantId, branch.Id, "07CCCCC2222C1Z2"); // Delhi "07" vs Mumbai "27" -> inter-state
        var entry = await SeedApprovedTimeEntryAsync(db, tenantId, matter.Id, 10000);
        var draft = await service.CreateDraftAsync(tenantId, matter.Id, new CreateInvoiceInput(null, 15, [entry.Id], null, null, null), CancellationToken.None);

        var sent = await service.SendAsync(tenantId, Guid.NewGuid(), draft.Id, CancellationToken.None);

        sent.Taxes.Should().ContainSingle(t => t.Name == "IGST" && t.Amount == 1800);
        sent.GrandTotal.Should().Be(11800);
    }

    [Fact]
    public async Task SendAsync_marks_the_pulled_time_entries_as_Billed()
    {
        await using var db = CreateContext(nameof(SendAsync_marks_the_pulled_time_entries_as_Billed));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();
        var (matter, _) = await SeedMatterAndClientAsync(db, tenantId, null, null);
        await db.TaxConfigs.AddAsync(new TaxConfig(tenantId, "IN", "GST", "{\"cgstPct\":9,\"sgstPct\":9,\"igstPct\":18}"));
        await db.SaveChangesAsync();
        var entry = await SeedApprovedTimeEntryAsync(db, tenantId, matter.Id, 5000);
        var draft = await service.CreateDraftAsync(tenantId, matter.Id, new CreateInvoiceInput(null, 15, [entry.Id], null, null, null), CancellationToken.None);

        await service.SendAsync(tenantId, Guid.NewGuid(), draft.Id, CancellationToken.None);

        (await db.TimeEntries.SingleAsync(e => e.Id == entry.Id)).Status.Should().Be("Billed");
    }

    [Fact]
    public async Task VoidAsync_throws_when_the_invoice_has_payments_applied()
    {
        await using var db = CreateContext(nameof(VoidAsync_throws_when_the_invoice_has_payments_applied));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();
        var (matter, client) = await SeedMatterAndClientAsync(db, tenantId, null, null);
        var invoice = new Invoice(tenantId, matter.Id, client.Id, DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow).AddDays(15), "INR", null);
        invoice.SetTotals(1000, 0, 0, 1000);
        invoice.MarkSent();
        invoice.ApplyPayment(500);
        await db.Invoices.AddAsync(invoice);
        await db.SaveChangesAsync();

        var act = () => service.VoidAsync(tenantId, Guid.NewGuid(), invoice.Id, "client changed mind", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetAgingAsync_buckets_sum_exactly_to_the_total_outstanding()
    {
        await using var db = CreateContext(nameof(GetAgingAsync_buckets_sum_exactly_to_the_total_outstanding));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();
        var (matter, client) = await SeedMatterAndClientAsync(db, tenantId, null, null);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var current = new Invoice(tenantId, matter.Id, client.Id, today, today.AddDays(10), "INR", null);
        current.SetTotals(1000, 0, 0, 1000);
        current.MarkSent();

        var overdue45 = new Invoice(tenantId, matter.Id, client.Id, today.AddDays(-60), today.AddDays(-45), "INR", null);
        overdue45.SetTotals(2000, 0, 0, 2000);
        overdue45.MarkSent();

        await db.Invoices.AddRangeAsync(current, overdue45);
        await db.SaveChangesAsync();

        var report = await service.GetAgingAsync(tenantId, today, CancellationToken.None);

        (report.Current + report.Bucket1To30 + report.Bucket31To60 + report.Bucket61To90 + report.Over90).Should().Be(report.Total);
        report.Total.Should().Be(3000);
        report.Current.Should().Be(1000);
        report.Bucket31To60.Should().Be(2000);
    }

    private sealed class FakeBlobStorageService : IBlobStorageService
    {
        private readonly Dictionary<string, byte[]> _store = [];

        public Task<string> UploadAsync(string container, string blobPath, byte[] content, string contentType, CancellationToken cancellationToken = default)
        {
            _store[$"{container}/{blobPath}"] = content;
            return Task.FromResult(blobPath);
        }

        public Task<byte[]> DownloadAsync(string container, string blobPath, CancellationToken cancellationToken = default)
            => Task.FromResult(_store[$"{container}/{blobPath}"]);

        public Task<string> GetDownloadUrlAsync(string container, string blobPath, TimeSpan validFor, CancellationToken cancellationToken = default)
            => Task.FromResult($"https://fake-blob.test/{container}/{blobPath}");
    }
}
