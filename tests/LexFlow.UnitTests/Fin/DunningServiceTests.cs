using FluentAssertions;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Fin;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Fin;

/// <summary>Module 8 User Flow #7: dunning reminder schedule + per-invoice mute.</summary>
public sealed class DunningServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private static async Task<Invoice> SeedOverdueInvoiceAsync(LexFlowDbContext db, Guid tenantId)
    {
        var invoice = new Invoice(tenantId, Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-40), DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10), "INR", null);
        invoice.SetTotals(5000, 0, 0, 5000);
        invoice.MarkSent();
        await db.Invoices.AddAsync(invoice);
        await db.SaveChangesAsync();
        return invoice;
    }

    [Fact]
    public async Task RunDueRemindersAsync_marks_a_past_due_sent_invoice_as_Overdue()
    {
        await using var db = CreateContext(nameof(RunDueRemindersAsync_marks_a_past_due_sent_invoice_as_Overdue));
        var service = new DunningService(db);
        var tenantId = Guid.NewGuid();
        var invoice = await SeedOverdueInvoiceAsync(db, tenantId);

        await service.RunDueRemindersAsync(CancellationToken.None);

        (await db.Invoices.SingleAsync(i => i.Id == invoice.Id)).Status.Should().Be("Overdue");
    }

    [Fact]
    public async Task RunDueRemindersAsync_schedules_and_sends_due_reminder_steps_from_the_active_schedule()
    {
        await using var db = CreateContext(nameof(RunDueRemindersAsync_schedules_and_sends_due_reminder_steps_from_the_active_schedule));
        var dunningService = new DunningService(db);
        var tenantId = Guid.NewGuid();
        var invoice = await SeedOverdueInvoiceAsync(db, tenantId);
        // "due-3" is 3 days before the due date, which for this invoice (due 10 days ago) is
        // already in the past — it should be scheduled and immediately marked Sent.
        await dunningService.UpsertScheduleAsync(tenantId, "Standard", "[{\"label\":\"due-3\",\"offsetDays\":-3,\"channel\":\"Email\"},{\"label\":\"+30\",\"offsetDays\":30,\"channel\":\"Email\"}]", true, CancellationToken.None);

        await dunningService.RunDueRemindersAsync(CancellationToken.None);

        var events = await db.DunningEvents.Where(e => e.InvoiceId == invoice.Id).ToListAsync();
        events.Should().HaveCount(2);
        events.Single(e => e.StepLabel == "due-3").Status.Should().Be("Sent");
        events.Single(e => e.StepLabel == "+30").Status.Should().Be("Pending"); // +30 days from due date is still in the future
    }

    [Fact]
    public async Task MuteAsync_prevents_a_pending_reminder_from_being_sent()
    {
        await using var db = CreateContext(nameof(MuteAsync_prevents_a_pending_reminder_from_being_sent));
        var dunningService = new DunningService(db);
        var tenantId = Guid.NewGuid();
        var invoice = await SeedOverdueInvoiceAsync(db, tenantId);
        await dunningService.UpsertScheduleAsync(tenantId, "Standard", "[{\"label\":\"due-3\",\"offsetDays\":-3,\"channel\":\"Email\"}]", true, CancellationToken.None);

        // First run creates the (already-due) event as Pending; mute it before the send pass would run again.
        await db.DunningEvents.AddAsync(new DunningEvent(tenantId, invoice.Id, null, "due-3", "Email", DateTimeOffset.UtcNow.AddDays(-1)));
        await db.SaveChangesAsync();

        await dunningService.MuteAsync(tenantId, invoice.Id, CancellationToken.None);
        await dunningService.RunDueRemindersAsync(CancellationToken.None);

        var events = await db.DunningEvents.Where(e => e.InvoiceId == invoice.Id).ToListAsync();
        events.Should().Contain(e => e.Muted && e.Status == "Pending");
    }
}
