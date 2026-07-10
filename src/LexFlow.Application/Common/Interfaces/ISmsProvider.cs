namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 11 SMS — one implementation per gateway (Twilio, MSG91), resolved by <see cref="Provider"/> name.</summary>
public interface ISmsProvider
{
    string Provider { get; }

    Task<SmsSendResult> SendAsync(Guid tenantId, string toNumber, string body, string? dltTemplateId, CancellationToken cancellationToken = default);
}

public sealed record SmsSendResult(bool Success, string? ProviderMessageId, string Status, string? Error);

/// <summary>
/// Domain-facing SMS send: resolves the tenant's configured provider, enforces AC-CM4
/// (India-compliance mode requires a DLT-registered template — hard block otherwise),
/// and logs every attempt to comm.sms_messages regardless of outcome.
/// </summary>
public interface ISmsService
{
    Task<SmsMessageDto> SendAsync(Guid tenantId, SendSmsInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SmsMessageDto>> GetForClientAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default);

    /// <summary>Records an inbound SMS from the webhook router into the client's timeline.</summary>
    Task<SmsMessageDto> RecordInboundAsync(Guid tenantId, string fromNumber, string toNumber, string body, string? providerMessageId, string provider, CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(Guid tenantId, string providerMessageId, string status, CancellationToken cancellationToken = default);
}

public sealed record SendSmsInput(Guid? ClientId, Guid? MatterId, string ToNumber, Guid? TemplateId, string? FreeformBody, IReadOnlyDictionary<string, string>? Variables);

public sealed record SmsMessageDto(Guid Id, Guid? ClientId, Guid? MatterId, string Direction, string? FromNumber, string? ToNumber, string? Body, string? DltTemplateId, string? Provider, string? ProviderMessageId, string Status, DateTimeOffset SentAt);
