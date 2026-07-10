using FluentAssertions;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Ops;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LexFlow.UnitTests.Ops;

/// <summary>§8 G-AC1/G-AC2, AC-CC3, §29 "nightly integrity job failures (page)" — nightly sweep run by LexFlow.Workers.</summary>
public sealed class AuditIntegrityCheckJobTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private static AuditIntegrityCheckJob CreateJob(LexFlowDbContext db) => new(db, NullLogger<AuditIntegrityCheckJob>.Instance);

    [Fact]
    public async Task RunAsync_flags_an_invoice_whose_AmountPaid_exceeds_its_GrandTotal()
    {
        await using var db = CreateContext(nameof(RunAsync_flags_an_invoice_whose_AmountPaid_exceeds_its_GrandTotal));
        var tenantId = Guid.NewGuid();
        var invoice = new Invoice(tenantId, Guid.NewGuid(), Guid.NewGuid(), null, null, "INR", null);
        invoice.SetTotals(1000, 0, 0, 1000);
        invoice.MarkSent();
        invoice.ApplyPayment(1200);
        await db.Invoices.AddAsync(invoice);
        await db.SaveChangesAsync();

        await CreateJob(db).RunAsync();

        var events = await db.AuditEvents.Where(e => e.TenantId == tenantId).ToListAsync();
        events.Should().ContainSingle();
        events[0].After.Should().Contain("G-AC1_INVOICE_OVERPAID");
    }

    [Fact]
    public async Task RunAsync_flags_negative_tenant_wide_AR_after_credit_notes_and_payments()
    {
        await using var db = CreateContext(nameof(RunAsync_flags_negative_tenant_wide_AR_after_credit_notes_and_payments));
        var tenantId = Guid.NewGuid();
        var invoice = new Invoice(tenantId, Guid.NewGuid(), Guid.NewGuid(), null, null, "INR", null);
        invoice.SetTotals(100, 0, 0, 100);
        invoice.MarkSent();
        invoice.ApplyPayment(60);
        var creditNote = new CreditNote(tenantId, "CN-1", invoice.Id, 50, "Billing error");
        creditNote.Issue();
        await db.Invoices.AddAsync(invoice);
        await db.CreditNotes.AddAsync(creditNote);
        await db.SaveChangesAsync();

        await CreateJob(db).RunAsync();

        var events = await db.AuditEvents.Where(e => e.TenantId == tenantId).ToListAsync();
        events.Should().ContainSingle();
        events[0].After.Should().Contain("G-AC1_NEGATIVE_AR");
    }

    [Fact]
    public async Task RunAsync_flags_a_future_scheduled_hearing_with_no_reminder_rows()
    {
        await using var db = CreateContext(nameof(RunAsync_flags_a_future_scheduled_hearing_with_no_reminder_rows));
        var tenantId = Guid.NewGuid();
        var courtCase = new CourtCase(tenantId, Guid.NewGuid(), Guid.NewGuid(), "Civil", "CS/1/2026", 2026, null, null, "Filed", null, null, null);
        var hearing = new Hearing(tenantId, courtCase.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), null, "Asia/Kolkata", "Arguments", null, Guid.NewGuid());
        await db.CourtCases.AddAsync(courtCase);
        await db.Hearings.AddAsync(hearing);
        await db.SaveChangesAsync();

        await CreateJob(db).RunAsync();

        var events = await db.AuditEvents.Where(e => e.TenantId == tenantId).ToListAsync();
        events.Should().ContainSingle();
        events[0].After.Should().Contain("G-AC2_HEARING_MISSING_REMINDERS");
    }

    [Fact]
    public async Task RunAsync_does_not_flag_a_future_hearing_that_already_has_reminder_rows()
    {
        await using var db = CreateContext(nameof(RunAsync_does_not_flag_a_future_hearing_that_already_has_reminder_rows));
        var tenantId = Guid.NewGuid();
        var courtCase = new CourtCase(tenantId, Guid.NewGuid(), Guid.NewGuid(), "Civil", "CS/2/2026", 2026, null, null, "Filed", null, null, null);
        var hearing = new Hearing(tenantId, courtCase.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), null, "Asia/Kolkata", "Arguments", null, Guid.NewGuid());
        var reminder = new EventReminder(tenantId, "hearing", hearing.Id, 0, "Email");
        await db.CourtCases.AddAsync(courtCase);
        await db.Hearings.AddAsync(hearing);
        await db.EventReminders.AddAsync(reminder);
        await db.SaveChangesAsync();

        await CreateJob(db).RunAsync();

        var events = await db.AuditEvents.Where(e => e.TenantId == tenantId).ToListAsync();
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_flags_an_Active_court_case_with_no_future_scheduled_hearing()
    {
        await using var db = CreateContext(nameof(RunAsync_flags_an_Active_court_case_with_no_future_scheduled_hearing));
        var tenantId = Guid.NewGuid();
        var courtCase = new CourtCase(tenantId, Guid.NewGuid(), Guid.NewGuid(), "Civil", "CS/3/2026", 2026, null, null, "Filed", null, null, null);
        await db.CourtCases.AddAsync(courtCase);
        await db.SaveChangesAsync();

        await CreateJob(db).RunAsync();

        var events = await db.AuditEvents.Where(e => e.TenantId == tenantId).ToListAsync();
        events.Should().ContainSingle();
        events[0].After.Should().Contain("AC-CC3_NO_FUTURE_HEARING");
    }

    [Fact]
    public async Task RunAsync_does_not_flag_a_Disposed_court_case_with_no_future_hearing()
    {
        await using var db = CreateContext(nameof(RunAsync_does_not_flag_a_Disposed_court_case_with_no_future_hearing));
        var tenantId = Guid.NewGuid();
        var courtCase = new CourtCase(tenantId, Guid.NewGuid(), Guid.NewGuid(), "Civil", "CS/4/2026", 2026, null, null, "Filed", null, null, null);
        courtCase.SetStatus("Disposed");
        await db.CourtCases.AddAsync(courtCase);
        await db.SaveChangesAsync();

        await CreateJob(db).RunAsync();

        var events = await db.AuditEvents.Where(e => e.TenantId == tenantId).ToListAsync();
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_writes_nothing_when_no_violations_exist()
    {
        await using var db = CreateContext(nameof(RunAsync_writes_nothing_when_no_violations_exist));

        await CreateJob(db).RunAsync();

        (await db.AuditEvents.ToListAsync()).Should().BeEmpty();
    }
}
