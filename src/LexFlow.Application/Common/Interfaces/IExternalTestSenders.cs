namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Live external calls behind interfaces (PRD Module 15: "Send Test Email" button /
/// SMS test send / WhatsApp test send / gateway verify) so they're mockable in tests
/// — no test in this codebase should ever hit a real SMTP host, SMS/WhatsApp API, or
/// payment gateway. Implementations live in Infrastructure; Application only depends
/// on these contracts.
/// </summary>
public interface ISmtpTestSender
{
    Task<ExternalCallResult> SendTestEmailAsync(SmtpConfig config, string toAddress, CancellationToken cancellationToken = default);
}

public interface ISmsTestSender
{
    Task<ExternalCallResult> SendTestSmsAsync(SmsGatewayConfig config, string toPhoneNumber, CancellationToken cancellationToken = default);
}

public interface IWhatsAppTestSender
{
    Task<ExternalCallResult> SendTestMessageAsync(WhatsAppConfig config, string toPhoneNumber, CancellationToken cancellationToken = default);

    Task<ExternalCallResult> SyncTemplatesAsync(WhatsAppConfig config, CancellationToken cancellationToken = default);
}

/// <summary>AC-S1: an invalid gateway credential must fail verification and therefore block save.</summary>
public interface IPaymentGatewayVerifier
{
    Task<ExternalCallResult> VerifyAsync(string provider, string configJson, string? secret, bool isTestMode, CancellationToken cancellationToken = default);
}

public sealed record ExternalCallResult(bool Success, string Message);

public sealed record SmtpConfig(string Host, int Port, string TlsMode, string? Username, string? Password, string FromName, string FromAddress);

public sealed record SmsGatewayConfig(string Provider, string? AccountSid, string? AuthToken, string SenderId, string? DltEntityId);

public sealed record WhatsAppConfig(string WabaId, string PhoneNumberId, string AccessToken);
