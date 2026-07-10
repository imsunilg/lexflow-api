using FluentAssertions;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Ops;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LexFlow.IntegrationTests.Ops;

/// <summary>
/// G-AC1 (§8): "For any tenant at any time: Σ(invoice totals) − Σ(credit notes) −
/// Σ(allocated payments) = total AR shown everywhere it appears... Nightly integrity job
/// asserts this; violation pages on-call." This exercises the exact job LexFlow.Workers runs
/// nightly (Infrastructure/Ops/AuditIntegrityCheckJob.cs — see its own unit-level counterpart,
/// tests/LexFlow.UnitTests/Ops/AuditIntegrityCheckJobTests.cs, which uses EF InMemory) against
/// a real Postgres container instead, so the reconciliation math runs on real `numeric(14,2)`
/// columns rather than InMemory's CLR-decimal semantics.
/// </summary>
[Collection(nameof(MoneyIntegrityCollection))]
public sealed class AuditIntegrityCheckJobIntegrationTests(MoneyIntegrityFixture fixture)
{
    private static LexFlowDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<LexFlowDbContext>().UseNpgsql(connectionString).Options);

    [Fact]
    public async Task RunAsync_pages_on_call_by_writing_a_Critical_audit_event_when_tenant_wide_AR_goes_negative()
    {
        await using var db = CreateContext(fixture.ConnectionString);
        var tenantId = Guid.NewGuid();

        var client = new Client(tenantId, "CL-GAC1-0001", "Individual", "GAC1", "Client", null, null, null, null, null, null, null, null);
        var matter = new Matter(tenantId, "MAT-GAC1-0001", "G-AC1 test matter", client.Id, "Litigation", null, null, null, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        db.AddRange(client, matter);
        await db.SaveChangesAsync();

        // Σ(GrandTotal) - Σ(credits) - Σ(AmountPaid) = 100 - 50 - 100 = -50: the invoice's own
        // open balance already hit zero from the payment, and a credit note was then also
        // issued on top of it — a real-world "issued a credit note after the invoice was
        // already fully paid, forgot to refund" money-integrity break.
        var invoice = new Invoice(tenantId, matter.Id, client.Id, null, null, "INR", null);
        invoice.SetTotals(100, 0, 0, 100);
        invoice.MarkSent();
        invoice.ApplyPayment(100);
        var creditNote = new CreditNote(tenantId, "CN-GAC1-0001", invoice.Id, 50, "Post-payment billing correction");
        creditNote.Issue();
        db.AddRange(invoice, creditNote);
        await db.SaveChangesAsync();

        var job = new AuditIntegrityCheckJob(db, NullLogger<AuditIntegrityCheckJob>.Instance);
        await job.RunAsync();

        var events = await db.AuditEvents.Where(e => e.TenantId == tenantId && e.Action == "integrity.violation").ToListAsync();
        events.Should().ContainSingle();
        events[0].ActorType.Should().Be("System");
        events[0].After.Should().Contain("G-AC1_NEGATIVE_AR");
    }

    [Fact]
    public async Task RunAsync_flags_an_individual_invoice_whose_recorded_payments_exceed_its_own_total()
    {
        await using var db = CreateContext(fixture.ConnectionString);
        var tenantId = Guid.NewGuid();

        var client = new Client(tenantId, "CL-GAC1-0002", "Individual", "GAC1b", "Client", null, null, null, null, null, null, null, null);
        var matter = new Matter(tenantId, "MAT-GAC1-0002", "G-AC1 overpaid matter", client.Id, "Litigation", null, null, null, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        db.AddRange(client, matter);
        await db.SaveChangesAsync();

        var invoice = new Invoice(tenantId, matter.Id, client.Id, null, null, "INR", null);
        invoice.SetTotals(50, 0, 0, 50);
        invoice.MarkSent();
        invoice.ApplyPayment(80);
        await db.Invoices.AddAsync(invoice);
        await db.SaveChangesAsync();

        var job = new AuditIntegrityCheckJob(db, NullLogger<AuditIntegrityCheckJob>.Instance);
        await job.RunAsync();

        var events = await db.AuditEvents.Where(e => e.TenantId == tenantId && e.Action == "integrity.violation").ToListAsync();
        events.Should().ContainSingle();
        events[0].After.Should().Contain("G-AC1_INVOICE_OVERPAID");
    }

    [Fact]
    public async Task RunAsync_writes_no_violation_when_money_reconciles_exactly()
    {
        await using var db = CreateContext(fixture.ConnectionString);
        var tenantId = Guid.NewGuid();

        var client = new Client(tenantId, "CL-GAC1-0003", "Individual", "GAC1c", "Client", null, null, null, null, null, null, null, null);
        var matter = new Matter(tenantId, "MAT-GAC1-0003", "G-AC1 clean matter", client.Id, "Litigation", null, null, null, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        db.AddRange(client, matter);
        await db.SaveChangesAsync();

        // GrandTotal 100, one 30-credit-note, a 70 payment: 100 - 30 - 70 = 0, exactly reconciled.
        var invoice = new Invoice(tenantId, matter.Id, client.Id, null, null, "INR", null);
        invoice.SetTotals(100, 0, 0, 100);
        invoice.MarkSent();
        invoice.ApplyPayment(70);
        var creditNote = new CreditNote(tenantId, "CN-GAC1-0002", invoice.Id, 30, "Partial write-off");
        creditNote.Issue();
        db.AddRange(invoice, creditNote);
        await db.SaveChangesAsync();

        var job = new AuditIntegrityCheckJob(db, NullLogger<AuditIntegrityCheckJob>.Instance);
        await job.RunAsync();

        (await db.AuditEvents.Where(e => e.TenantId == tenantId && e.Action == "integrity.violation").ToListAsync()).Should().BeEmpty();
    }
}
