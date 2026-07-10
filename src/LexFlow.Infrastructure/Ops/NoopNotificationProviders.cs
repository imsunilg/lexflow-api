using LexFlow.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace LexFlow.Infrastructure.Ops;

/// <summary>
/// §22/§23 build note: "interfaces only here; concrete providers wired in C-8/C-13." These
/// log-and-succeed stand-ins let NotificationService's fallback-chain, quiet-hours, and
/// preference-narrowing logic run and be tested end-to-end today; swapping in a real
/// SendGrid/Twilio/WhatsApp Cloud API/FCM client later is a DI registration change only —
/// no caller of INotificationService needs to change.
/// </summary>
public sealed class NoopEmailNotificationProvider(ILogger<NoopEmailNotificationProvider> logger) : IEmailNotificationProvider
{
    public Task<bool> SendAsync(string toEmail, string title, string? body, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Email notification (no-op provider): to={ToEmail} title={Title}", toEmail, title);
        return Task.FromResult(true);
    }
}

public sealed class NoopSmsNotificationProvider(ILogger<NoopSmsNotificationProvider> logger) : ISmsNotificationProvider
{
    public Task<bool> SendAsync(string toPhone, string title, string? body, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("SMS notification (no-op provider): to={ToPhone} title={Title}", toPhone, title);
        return Task.FromResult(true);
    }
}

public sealed class NoopWhatsAppNotificationProvider(ILogger<NoopWhatsAppNotificationProvider> logger) : IWhatsAppNotificationProvider
{
    public Task<bool> SendAsync(string toPhone, string title, string? body, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("WhatsApp notification (no-op provider): to={ToPhone} title={Title}", toPhone, title);
        return Task.FromResult(true);
    }
}

public sealed class NoopPushNotificationProvider(ILogger<NoopPushNotificationProvider> logger) : IPushNotificationProvider
{
    public Task<bool> SendAsync(Guid userId, string title, string? body, string? deepLink, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Push notification (no-op provider): userId={UserId} title={Title}", userId, title);
        return Task.FromResult(true);
    }
}
