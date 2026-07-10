using FluentAssertions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Ops;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Ops;

/// <summary>
/// §22 Notification Matrix: always-write in-app row, WhatsApp-&gt;SMS-&gt;Email failure
/// fallback chain, quiet-hours suppression (mandatory bypasses it), and per-kind
/// preference overrides. Quiet-hours windows in these tests are deliberately extreme
/// (near-all-day or near-zero) so the assertions hold regardless of the real wall-clock
/// time the test suite happens to run at — there's no injectable clock in
/// NotificationService, matching the rest of this codebase's "system time is
/// authoritative" convention.
/// </summary>
public sealed class NotificationServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private static async Task<(LexFlowDbContext Db, Guid TenantId, User User)> SeedUserAsync(string dbName, string? notificationPrefsJson = null)
    {
        var db = CreateContext(dbName);
        var tenantId = Guid.NewGuid();
        var user = new User(tenantId, "lawyer@example.com", "Test Lawyer");
        if (notificationPrefsJson is not null)
        {
            user.UpdateProfile(user.Name, null, null, user.Phone, null, null, null, user.Tz, user.Locale, notificationPrefsJson);
        }

        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
        return (db, tenantId, user);
    }

    [Fact]
    public async Task NotifyAsync_always_writes_an_in_app_row_regardless_of_requested_channels()
    {
        var (db, tenantId, user) = await SeedUserAsync(nameof(NotifyAsync_always_writes_an_in_app_row_regardless_of_requested_channels));
        await using var _ = db;
        var (service, email, sms, whatsApp, push) = CreateService(db);

        await service.NotifyAsync(tenantId, user.Id, new NotifyRequest("task.assigned", "New task", "body", null, ["Email"]), CancellationToken.None);

        var stored = await db.Notifications.SingleAsync();
        stored.Title.Should().Be("New task");
    }

    [Fact]
    public async Task NotifyAsync_falls_back_WhatsApp_to_SMS_to_Email_when_WhatsApp_fails()
    {
        var (db, tenantId, user) = await SeedUserAsync(nameof(NotifyAsync_falls_back_WhatsApp_to_SMS_to_Email_when_WhatsApp_fails));
        user.UpdateProfile(user.Name, user.Designation, user.BarEnrollmentNo, "+919999999999", null, null, null, user.Tz, user.Locale, user.NotificationPrefs);
        await db.SaveChangesAsync();
        await using var _ = db;
        var (service, email, sms, whatsApp, push) = CreateService(db, whatsAppSucceeds: false, smsSucceeds: false);

        await service.NotifyAsync(tenantId, user.Id, new NotifyRequest("invoice.sent", "Invoice", null, null, ["WhatsApp"]), CancellationToken.None);

        whatsApp.Attempts.Should().Be(1);
        sms.Attempts.Should().Be(1, "WhatsApp failed, so SMS is the next link in the fallback chain");
        email.Attempts.Should().Be(1, "SMS also failed, so Email is the final fallback");
    }

    [Fact]
    public async Task NotifyAsync_does_not_fall_back_when_WhatsApp_succeeds()
    {
        var (db, tenantId, user) = await SeedUserAsync(nameof(NotifyAsync_does_not_fall_back_when_WhatsApp_succeeds));
        user.UpdateProfile(user.Name, user.Designation, user.BarEnrollmentNo, "+919999999999", null, null, null, user.Tz, user.Locale, user.NotificationPrefs);
        await db.SaveChangesAsync();
        await using var _ = db;
        var (service, email, sms, whatsApp, push) = CreateService(db, whatsAppSucceeds: true);

        await service.NotifyAsync(tenantId, user.Id, new NotifyRequest("invoice.sent", "Invoice", null, null, ["WhatsApp"]), CancellationToken.None);

        whatsApp.Attempts.Should().Be(1);
        sms.Attempts.Should().Be(0);
        email.Attempts.Should().Be(0);
    }

    [Fact]
    public async Task NotifyAsync_suppresses_non_mandatory_channels_during_quiet_hours_but_still_writes_the_in_app_row()
    {
        // Window spans nearly the entire day -> "now" (in UTC, since Tz is unset) is
        // virtually certain to fall inside it, regardless of when this test runs.
        var (db, tenantId, user) = await SeedUserAsync(
            nameof(NotifyAsync_suppresses_non_mandatory_channels_during_quiet_hours_but_still_writes_the_in_app_row),
            "{\"quietHours\":{\"start\":\"00:00\",\"end\":\"23:59\"}}");
        await using var _ = db;
        var (service, email, sms, whatsApp, push) = CreateService(db);

        var result = await service.NotifyAsync(tenantId, user.Id, new NotifyRequest("task.assigned", "New task", null, null, ["Email", "Push"]), CancellationToken.None);

        result.ChannelsSkippedQuietHours.Should().Contain(["Email", "Push"]);
        email.Attempts.Should().Be(0);
        push.Attempts.Should().Be(0);
        (await db.Notifications.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task NotifyAsync_mandatory_class_ignores_quiet_hours()
    {
        var (db, tenantId, user) = await SeedUserAsync(
            nameof(NotifyAsync_mandatory_class_ignores_quiet_hours),
            "{\"quietHours\":{\"start\":\"00:00\",\"end\":\"23:59\"}}");
        await using var _ = db;
        var (service, email, sms, whatsApp, push) = CreateService(db);

        var result = await service.NotifyAsync(tenantId, user.Id, new NotifyRequest("limitation.escalation", "Limitation date", null, null, ["Email"], Mandatory: true), CancellationToken.None);

        result.ChannelsSkippedQuietHours.Should().BeEmpty();
        email.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task NotifyAsync_narrows_channels_by_the_users_per_kind_preference_override()
    {
        var (db, tenantId, user) = await SeedUserAsync(
            nameof(NotifyAsync_narrows_channels_by_the_users_per_kind_preference_override),
            "{\"overrides\":{\"task.overdue\":[\"InApp\"]}}");
        await using var _ = db;
        var (service, email, sms, whatsApp, push) = CreateService(db);

        await service.NotifyAsync(tenantId, user.Id, new NotifyRequest("task.overdue", "Overdue", null, null, ["Email", "Push"]), CancellationToken.None);

        email.Attempts.Should().Be(0, "the user's override for this kind restricts delivery to InApp only");
        push.Attempts.Should().Be(0);
    }

    [Fact]
    public async Task MarkReadAsync_sets_ReadAt_and_GetForUserAsync_unreadOnly_then_excludes_it()
    {
        var (db, tenantId, user) = await SeedUserAsync(nameof(MarkReadAsync_sets_ReadAt_and_GetForUserAsync_unreadOnly_then_excludes_it));
        await using var _ = db;
        var (service, _, _, _, _) = CreateService(db);
        var result = await service.NotifyAsync(tenantId, user.Id, new NotifyRequest("task.assigned", "New task", null, null, ["InApp"]), CancellationToken.None);

        (await service.GetForUserAsync(tenantId, user.Id, unreadOnly: true, CancellationToken.None)).Should().ContainSingle();

        await service.MarkReadAsync(tenantId, user.Id, result.NotificationId, CancellationToken.None);

        (await service.GetForUserAsync(tenantId, user.Id, unreadOnly: true, CancellationToken.None)).Should().BeEmpty();
    }

    private static (NotificationService Service, FakeChannelProvider Email, FakeChannelProvider Sms, FakeChannelProvider WhatsApp, FakePushProvider Push) CreateService(
        LexFlowDbContext db, bool whatsAppSucceeds = true, bool smsSucceeds = true)
    {
        var email = new FakeChannelProvider(true);
        var sms = new FakeChannelProvider(smsSucceeds);
        var whatsApp = new FakeChannelProvider(whatsAppSucceeds);
        var push = new FakePushProvider();
        var service = new NotificationService(db, email, sms, whatsApp, push);
        return (service, email, sms, whatsApp, push);
    }

    private sealed class FakeChannelProvider(bool succeeds) : IEmailNotificationProvider, ISmsNotificationProvider, IWhatsAppNotificationProvider
    {
        public int Attempts { get; private set; }

        public Task<bool> SendAsync(string toEmail, string title, string? body, CancellationToken cancellationToken = default)
        {
            Attempts++;
            return Task.FromResult(succeeds);
        }
    }

    private sealed class FakePushProvider : IPushNotificationProvider
    {
        public int Attempts { get; private set; }

        public Task<bool> SendAsync(Guid userId, string title, string? body, string? deepLink, CancellationToken cancellationToken = default)
        {
            Attempts++;
            return Task.FromResult(true);
        }
    }
}
