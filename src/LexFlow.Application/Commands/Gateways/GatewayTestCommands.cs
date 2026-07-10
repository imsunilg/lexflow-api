using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Gateways;

/// <summary>POST /api/v1/settings/smtp/test — "Send Test Email" (PRD Module 15 §4).</summary>
public sealed record TestSmtpCommand(string Host, int Port, string TlsMode, string? Username, string? Password, string FromName, string FromAddress, string ToAddress)
    : IRequest<ExternalCallResult>;

public sealed class TestSmtpCommandHandler(ISmtpTestSender smtpTestSender) : IRequestHandler<TestSmtpCommand, ExternalCallResult>
{
    public Task<ExternalCallResult> Handle(TestSmtpCommand request, CancellationToken cancellationToken)
        => smtpTestSender.SendTestEmailAsync(
            new SmtpConfig(request.Host, request.Port, request.TlsMode, request.Username, request.Password, request.FromName, request.FromAddress),
            request.ToAddress,
            cancellationToken);
}

/// <summary>POST /api/v1/settings/sms/test (PRD Module 15 §5).</summary>
public sealed record TestSmsCommand(string Provider, string? AccountSid, string? AuthToken, string SenderId, string? DltEntityId, string ToPhoneNumber)
    : IRequest<ExternalCallResult>;

public sealed class TestSmsCommandHandler(ISmsTestSender smsTestSender) : IRequestHandler<TestSmsCommand, ExternalCallResult>
{
    public Task<ExternalCallResult> Handle(TestSmsCommand request, CancellationToken cancellationToken)
        => smsTestSender.SendTestSmsAsync(
            new SmsGatewayConfig(request.Provider, request.AccountSid, request.AuthToken, request.SenderId, request.DltEntityId),
            request.ToPhoneNumber,
            cancellationToken);
}

/// <summary>POST /api/v1/settings/whatsapp/sync-templates (PRD Module 15 §6).</summary>
public sealed record SyncWhatsAppTemplatesCommand(string WabaId, string PhoneNumberId, string AccessToken) : IRequest<ExternalCallResult>;

public sealed class SyncWhatsAppTemplatesCommandHandler(IWhatsAppTestSender whatsAppTestSender) : IRequestHandler<SyncWhatsAppTemplatesCommand, ExternalCallResult>
{
    public Task<ExternalCallResult> Handle(SyncWhatsAppTemplatesCommand request, CancellationToken cancellationToken)
        => whatsAppTestSender.SyncTemplatesAsync(new WhatsAppConfig(request.WabaId, request.PhoneNumberId, request.AccessToken), cancellationToken);
}

/// <summary>POST /api/v1/settings/whatsapp/test (test send to a verified number, PRD Module 15 §6).</summary>
public sealed record TestWhatsAppCommand(string WabaId, string PhoneNumberId, string AccessToken, string ToPhoneNumber) : IRequest<ExternalCallResult>;

public sealed class TestWhatsAppCommandHandler(IWhatsAppTestSender whatsAppTestSender) : IRequestHandler<TestWhatsAppCommand, ExternalCallResult>
{
    public Task<ExternalCallResult> Handle(TestWhatsAppCommand request, CancellationToken cancellationToken)
        => whatsAppTestSender.SendTestMessageAsync(new WhatsAppConfig(request.WabaId, request.PhoneNumberId, request.AccessToken), request.ToPhoneNumber, cancellationToken);
}

/// <summary>
/// POST /api/v1/settings/gateways/{g}/verify (PRD Module 15 §7). AC-S1: an invalid
/// credential fails verification and therefore blocks save — this command only
/// verifies; the controller only calls IGatewayConfigService.UpsertAsync afterward
/// if verification succeeded.
/// </summary>
public sealed record VerifyPaymentGatewayCommand(string Provider, string ConfigJson, string? Secret, bool IsTestMode) : IRequest<ExternalCallResult>;

public sealed class VerifyPaymentGatewayCommandHandler(IPaymentGatewayVerifier verifier) : IRequestHandler<VerifyPaymentGatewayCommand, ExternalCallResult>
{
    public Task<ExternalCallResult> Handle(VerifyPaymentGatewayCommand request, CancellationToken cancellationToken)
        => verifier.VerifyAsync(request.Provider, request.ConfigJson, request.Secret, request.IsTestMode, cancellationToken);
}

/// <summary>Persists a payment gateway config after AC-S1 verification succeeded.</summary>
public sealed record SavePaymentGatewayCommand(string Provider, string ConfigJson, string? Secret, bool IsEnabled, bool IsTestMode) : IRequest<GatewayConfigDto>;

public sealed class SavePaymentGatewayCommandHandler(IGatewayConfigService gatewayConfigService, ICurrentUserService currentUser)
    : IRequestHandler<SavePaymentGatewayCommand, GatewayConfigDto>
{
    public Task<GatewayConfigDto> Handle(SavePaymentGatewayCommand request, CancellationToken cancellationToken)
        => gatewayConfigService.UpsertAsync(currentUser.TenantId!.Value, request.Provider, request.ConfigJson, request.Secret, request.IsEnabled, request.IsTestMode, cancellationToken);
}
