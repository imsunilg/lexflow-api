namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 8 User Flow #5: "payment link embedded (Stripe/Razorpay/PayPal per firm config; UPI
/// intent on Razorpay)." One implementation per provider (Provider discriminates), credentials
/// resolved the same way as every other "conditional external" gateway in this codebase (core.
/// gateway_configs + Key Vault, via GatewayCredentialResolver) — see Stripe/Razorpay/PayPal
/// PaymentGateway classes. Capture/webhook handling itself is not part of this interface: it comes
/// in through the shared IWebhookRouter (§34) and is applied via IPaymentService.HandleGatewayCapturedAsync.
/// </summary>
public interface IPaymentGateway
{
    string Provider { get; }

    Task<PaymentLinkResult> CreatePaymentLinkAsync(Guid tenantId, Guid invoiceId, decimal amount, string currency, string description, CancellationToken cancellationToken = default);

    /// <summary>
    /// Module 17 Pay-Now return-URL reconciliation: queries the gateway directly for a
    /// checkout/payment-link's current status rather than waiting on its webhook, so the
    /// return-URL handler can complete reconciliation "server-side regardless" of whether the
    /// webhook has arrived yet (PRD Module 17 Error Handling).
    /// </summary>
    Task<GatewayPaymentStatusResult> GetPaymentStatusAsync(Guid tenantId, string gatewayRef, CancellationToken cancellationToken = default);
}

public sealed record PaymentLinkResult(bool Success, string? Url, string? GatewayRef, string? Error);

public sealed record GatewayPaymentStatusResult(bool Captured, decimal? CapturedAmount, string? Error);
