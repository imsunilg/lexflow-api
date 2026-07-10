namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 11 WhatsApp Cloud API — session (free-form, 24h window) vs template (HSM) sends.</summary>
public interface IWhatsAppProvider
{
    Task<WhatsAppSendResult> SendSessionMessageAsync(Guid tenantId, string toPhoneE164, string body, CancellationToken cancellationToken = default);

    Task<WhatsAppSendResult> SendTemplateMessageAsync(Guid tenantId, string toPhoneE164, string hsmName, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken = default);
}

public sealed record WhatsAppSendResult(bool Success, string? WaMsgId, string Status, string? Error);

/// <summary>
/// Domain-facing WhatsApp send: AC-CM2 (template delivers, status updates via webhook),
/// Module 11 validation ("WhatsApp template send requires opt-in record"; "session
/// message blocked outside 24-h window with template-suggestion").
/// </summary>
public interface IWhatsAppService
{
    Task<WhatsappMessageDto> SendAsync(Guid tenantId, SendWhatsAppInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WhatsappMessageDto>> GetForClientAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default);

    Task<bool> HasActiveOptInAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default);

    Task<WhatsAppOptinDto> OptInAsync(Guid tenantId, Guid clientId, string phoneE164, string? source, CancellationToken cancellationToken = default);

    Task OptOutAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default);

    /// <summary>Records an inbound WhatsApp message (extends the 24h session window for this client) from the webhook router.</summary>
    Task<WhatsappMessageDto> RecordInboundAsync(Guid tenantId, Guid? clientId, string waMsgId, string? body, CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(Guid tenantId, string waMsgId, string status, CancellationToken cancellationToken = default);
}

public sealed record SendWhatsAppInput(Guid ClientId, Guid? TemplateId, IReadOnlyDictionary<string, string>? Variables, string? SessionText);

public sealed record WhatsappMessageDto(Guid Id, Guid? ClientId, string WaMsgId, string Direction, Guid? TemplateId, string? Body, string Status, DateTimeOffset? WindowExpiresAt);

public sealed record WhatsAppOptinDto(Guid Id, Guid ClientId, string PhoneE164, DateTimeOffset OptedInAt, DateTimeOffset? OptedOutAt);
