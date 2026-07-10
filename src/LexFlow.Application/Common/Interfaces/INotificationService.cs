namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// §22 Notification Matrix — unified fan-out across Email/SMS/WhatsApp/Push/In-app.
/// Always writes an ops.notifications row (the in-app channel doubles as the audit record
/// of "what was sent"); other channels are attempted per the event's channel list, subject
/// to per-user preference overrides (core.users.notification_prefs), quiet hours
/// (22:00-07:00 user TZ, bypassed only when <paramref name="isMandatory"/> is true), and a
/// failure fallback chain (WhatsApp -> SMS -> Email) recorded in ops.reminder_dispatch_log
/// when this notification is reminder-sourced.
/// </summary>
public interface INotificationService
{
    Task<NotificationDispatchResult> NotifyAsync(Guid tenantId, Guid userId, NotifyRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationDto>> GetForUserAsync(Guid tenantId, Guid userId, bool unreadOnly, CancellationToken cancellationToken = default);

    Task MarkReadAsync(Guid tenantId, Guid userId, Guid notificationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// <paramref name="Channels"/> is the event's default channel list per §22 (e.g. ["Email","Push","InApp"]);
/// the service narrows it by the user's preference overrides unless <paramref name="Mandatory"/>.
/// </summary>
public sealed record NotifyRequest(string Kind, string Title, string? Body, string? DeepLink, IReadOnlyList<string> Channels, bool Mandatory = false);

public sealed record NotificationDispatchResult(Guid NotificationId, IReadOnlyList<string> ChannelsAttempted, IReadOnlyList<string> ChannelsSkippedQuietHours);

public sealed record NotificationDto(Guid Id, string Kind, string Title, string? Body, string? DeepLink, DateTimeOffset? ReadAt, DateTimeOffset CreatedAt);

/// <summary>One interface per §22 channel; Email/SMS/WhatsApp/Push are interface-only here (real Twilio/SendGrid/WhatsApp Cloud API/FCM wiring lands in C-8/C-13) — DI registers a logging no-op so the fallback-chain logic in NotificationService is fully exercised today.</summary>
public interface IEmailNotificationProvider
{
    Task<bool> SendAsync(string toEmail, string title, string? body, CancellationToken cancellationToken = default);
}

public interface ISmsNotificationProvider
{
    Task<bool> SendAsync(string toPhone, string title, string? body, CancellationToken cancellationToken = default);
}

public interface IWhatsAppNotificationProvider
{
    Task<bool> SendAsync(string toPhone, string title, string? body, CancellationToken cancellationToken = default);
}

public interface IPushNotificationProvider
{
    Task<bool> SendAsync(Guid userId, string title, string? body, string? deepLink, CancellationToken cancellationToken = default);
}
