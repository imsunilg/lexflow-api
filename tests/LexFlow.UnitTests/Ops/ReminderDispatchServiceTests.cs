using FluentAssertions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Ops;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Ops;

/// <summary>AC-CAL2: a due hearing reminder dispatches and is proven by a reminder_dispatch_log row; a not-yet-due reminder is left Pending.</summary>
public sealed class ReminderDispatchServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task DispatchDueAsync_fires_a_hearing_reminder_once_the_offset_window_is_reached_and_logs_it()
    {
        await using var db = CreateContext(nameof(DispatchDueAsync_fires_a_hearing_reminder_once_the_offset_window_is_reached_and_logs_it));
        var tenantId = Guid.NewGuid();
        var lawyerId = Guid.NewGuid();
        await db.Users.AddAsync(new User(tenantId, "lawyer@example.com", "Test Lawyer"));

        var court = new Court(tenantId, "District Court", "Civil", "Mumbai", "Maharashtra", null);
        await db.Courts.AddAsync(court);
        var client = new Client(tenantId, "CL-1", "Individual", "Priya", "Shah", null, null, null, null, null, null, null, null);
        await db.Clients.AddAsync(client);
        var matter = new Matter(tenantId, "MAT-1", "Test matter", client.Id, "Litigation", null, null, null, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        await db.Matters.AddAsync(matter);
        var courtCase = new CourtCase(tenantId, matter.Id, court.Id, "Civil", "CC-1", 2026, null, DateOnly.FromDateTime(DateTime.UtcNow), null, null, null, null);
        await db.CourtCases.AddAsync(courtCase);
        await db.SaveChangesAsync();

        // A hearing happening right now: an offset-0 ("day-of") reminder is due immediately.
        var hearing = new Hearing(tenantId, courtCase.Id, DateOnly.FromDateTime(DateTime.UtcNow), TimeOnly.FromDateTime(DateTime.UtcNow), "Asia/Kolkata", "Arguments", "1", lawyerId);
        await db.Hearings.AddAsync(hearing);
        await db.SaveChangesAsync();

        var reminder = new EventReminder(tenantId, "hearing", hearing.Id, 0, "Email");
        await db.EventReminders.AddAsync(reminder);
        await db.SaveChangesAsync();

        var service = new ReminderDispatchService(db, new NotificationService(db, new NoopEmail(), new NoopSms(), new NoopWhatsApp(), new NoopPush()));
        await service.DispatchDueAsync(CancellationToken.None);

        var reloadedReminder = await db.EventReminders.SingleAsync(r => r.Id == reminder.Id);
        reloadedReminder.Status.Should().Be("Sent");

        var log = await db.ReminderDispatchLogs.SingleAsync(l => l.ReminderId == reminder.Id);
        log.Channel.Should().Be("Email");
        log.Status.Should().Be("Sent");
    }

    [Fact]
    public async Task DispatchDueAsync_leaves_a_not_yet_due_reminder_Pending()
    {
        await using var db = CreateContext(nameof(DispatchDueAsync_leaves_a_not_yet_due_reminder_Pending));
        var tenantId = Guid.NewGuid();
        var lawyerId = Guid.NewGuid();
        await db.Users.AddAsync(new User(tenantId, "lawyer@example.com", "Test Lawyer"));

        var court = new Court(tenantId, "District Court", "Civil", "Mumbai", "Maharashtra", null);
        await db.Courts.AddAsync(court);
        var client = new Client(tenantId, "CL-1", "Individual", "Priya", "Shah", null, null, null, null, null, null, null, null);
        await db.Clients.AddAsync(client);
        var matter = new Matter(tenantId, "MAT-1", "Test matter", client.Id, "Litigation", null, null, null, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        await db.Matters.AddAsync(matter);
        var courtCase = new CourtCase(tenantId, matter.Id, court.Id, "Civil", "CC-1", 2026, null, DateOnly.FromDateTime(DateTime.UtcNow), null, null, null, null);
        await db.CourtCases.AddAsync(courtCase);
        await db.SaveChangesAsync();

        // Hearing 30 days out; a 1-day-before reminder isn't due yet.
        var hearing = new Hearing(tenantId, courtCase.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), new TimeOnly(10, 0), "Asia/Kolkata", "Arguments", "1", lawyerId);
        await db.Hearings.AddAsync(hearing);
        await db.SaveChangesAsync();

        var reminder = new EventReminder(tenantId, "hearing", hearing.Id, 1_440, "Email");
        await db.EventReminders.AddAsync(reminder);
        await db.SaveChangesAsync();

        var service = new ReminderDispatchService(db, new NotificationService(db, new NoopEmail(), new NoopSms(), new NoopWhatsApp(), new NoopPush()));
        await service.DispatchDueAsync(CancellationToken.None);

        var reloadedReminder = await db.EventReminders.SingleAsync(r => r.Id == reminder.Id);
        reloadedReminder.Status.Should().Be("Pending");
        (await db.ReminderDispatchLogs.CountAsync()).Should().Be(0);
    }

    private sealed class NoopEmail : IEmailNotificationProvider
    {
        public Task<bool> SendAsync(string toEmail, string title, string? body, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class NoopSms : ISmsNotificationProvider
    {
        public Task<bool> SendAsync(string toPhone, string title, string? body, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class NoopWhatsApp : IWhatsAppNotificationProvider
    {
        public Task<bool> SendAsync(string toPhone, string title, string? body, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class NoopPush : IPushNotificationProvider
    {
        public Task<bool> SendAsync(Guid userId, string title, string? body, string? deepLink, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
