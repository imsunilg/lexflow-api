using System.Text.Json;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Ops;

/// <summary>
/// §22 Notification Matrix. Always writes an ops.notifications row (the in-app channel
/// doubles as the durable "what was sent" record); other channels are attempted per the
/// event's default channel list narrowed by core.users.notification_prefs overrides,
/// unless the class is <see cref="NotifyRequest.Mandatory"/> (limitation T-0, trust
/// events, security alerts — §22: "cannot disable"). Quiet hours (22:00-07:00 user TZ)
/// suppress non-mandatory Email/SMS/WhatsApp/Push (in-app never suppressed — it's the
/// passive inbox, not an interruption). WhatsApp failures fall back to SMS then Email
/// per the documented W->S->E chain.
/// </summary>
public sealed class NotificationService(
    LexFlowDbContext db,
    IEmailNotificationProvider emailProvider,
    ISmsNotificationProvider smsProvider,
    IWhatsAppNotificationProvider whatsAppProvider,
    IPushNotificationProvider pushProvider) : INotificationService
{
    public async Task<NotificationDispatchResult> NotifyAsync(Guid tenantId, Guid userId, NotifyRequest request, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId, cancellationToken);
        var prefs = ParsePreferences(user?.NotificationPrefs);

        var effectiveChannels = request.Mandatory
            ? request.Channels
            : NarrowByPreference(request.Channels, request.Kind, prefs);

        var inQuietHours = !request.Mandatory && IsInQuietHours(user?.Tz, prefs);
        var skippedForQuietHours = new List<string>();
        var attempted = new List<string>();

        var channelsJson = JsonSerializer.Serialize(request.Channels);
        var notification = new Notification(tenantId, userId, request.Kind, request.Title, request.Body, request.DeepLink, channelsJson);
        await db.Notifications.AddAsync(notification, cancellationToken);
        attempted.Add("InApp");

        foreach (var channel in effectiveChannels.Where(c => c != "InApp"))
        {
            if (inQuietHours)
            {
                skippedForQuietHours.Add(channel);
                continue;
            }

            attempted.Add(channel);
            await DispatchChannelAsync(channel, user, request, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new NotificationDispatchResult(notification.Id, attempted, skippedForQuietHours);
    }

    private async Task DispatchChannelAsync(string channel, User? user, NotifyRequest request, CancellationToken cancellationToken)
    {
        if (user is null)
        {
            return;
        }

        switch (channel)
        {
            case "Email":
                await emailProvider.SendAsync(user.Email, request.Title, request.Body, cancellationToken);
                break;
            case "SMS":
                if (user.Phone is not null)
                {
                    await smsProvider.SendAsync(user.Phone, request.Title, request.Body, cancellationToken);
                }

                break;
            case "WhatsApp":
                var whatsAppSent = user.Phone is not null && await whatsAppProvider.SendAsync(user.Phone, request.Title, request.Body, cancellationToken);
                if (!whatsAppSent)
                {
                    // §22 failure fallback chain: WhatsApp -> SMS -> Email.
                    var smsSent = user.Phone is not null && await smsProvider.SendAsync(user.Phone, request.Title, request.Body, cancellationToken);
                    if (!smsSent)
                    {
                        await emailProvider.SendAsync(user.Email, request.Title, request.Body, cancellationToken);
                    }
                }

                break;
            case "Push":
                await pushProvider.SendAsync(user.Id, request.Title, request.Body, request.DeepLink, cancellationToken);
                break;
        }
    }

    public async Task<IReadOnlyList<NotificationDto>> GetForUserAsync(Guid tenantId, Guid userId, bool unreadOnly, CancellationToken cancellationToken = default)
    {
        var query = db.Notifications.Where(n => n.TenantId == tenantId && n.UserId == userId);
        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAt == null);
        }

        var notifications = await query.OrderByDescending(n => n.CreatedAt).ToListAsync(cancellationToken);
        return notifications.Select(n => new NotificationDto(n.Id, n.Kind, n.Title, n.Body, n.DeepLink, n.ReadAt, n.CreatedAt)).ToList();
    }

    public async Task MarkReadAsync(Guid tenantId, Guid userId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(n => n.TenantId == tenantId && n.UserId == userId && n.Id == notificationId, cancellationToken);
        notification?.MarkRead();
        if (notification is not null)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static IReadOnlyList<string> NarrowByPreference(IReadOnlyList<string> defaultChannels, string kind, NotificationPreferences prefs)
        => prefs.Overrides.TryGetValue(kind, out var overrideChannels) ? overrideChannels : defaultChannels;

    private static bool IsInQuietHours(string? tz, NotificationPreferences prefs)
    {
        TimeZoneInfo timeZone;
        try
        {
            timeZone = string.IsNullOrWhiteSpace(tz) ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(tz);
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            timeZone = TimeZoneInfo.Utc;
        }

        var localTime = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).DateTime);
        var start = prefs.QuietHoursStart;
        var end = prefs.QuietHoursEnd;

        return start > end
            ? localTime >= start || localTime < end
            : localTime >= start && localTime < end;
    }

    private static NotificationPreferences ParsePreferences(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
        {
            return NotificationPreferences.Default;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var overrides = new Dictionary<string, IReadOnlyList<string>>();
            if (root.TryGetProperty("overrides", out var overridesElement))
            {
                foreach (var prop in overridesElement.EnumerateObject())
                {
                    overrides[prop.Name] = prop.Value.EnumerateArray().Select(v => v.GetString()!).ToList();
                }
            }

            var start = NotificationPreferences.Default.QuietHoursStart;
            var end = NotificationPreferences.Default.QuietHoursEnd;
            if (root.TryGetProperty("quietHours", out var quietHoursElement))
            {
                if (quietHoursElement.TryGetProperty("start", out var startProp) && TimeOnly.TryParse(startProp.GetString(), out var parsedStart))
                {
                    start = parsedStart;
                }

                if (quietHoursElement.TryGetProperty("end", out var endProp) && TimeOnly.TryParse(endProp.GetString(), out var parsedEnd))
                {
                    end = parsedEnd;
                }
            }

            return new NotificationPreferences(overrides, start, end);
        }
        catch (JsonException)
        {
            return NotificationPreferences.Default;
        }
    }
}

/// <summary>Parsed shape of core.users.notification_prefs: {"overrides": {"task.overdue": ["Email","InApp"]}, "quietHours": {"start": "22:00", "end": "07:00"}}.</summary>
public sealed record NotificationPreferences(IReadOnlyDictionary<string, IReadOnlyList<string>> Overrides, TimeOnly QuietHoursStart, TimeOnly QuietHoursEnd)
{
    public static NotificationPreferences Default { get; } = new(new Dictionary<string, IReadOnlyList<string>>(), new TimeOnly(22, 0), new TimeOnly(7, 0));
}
