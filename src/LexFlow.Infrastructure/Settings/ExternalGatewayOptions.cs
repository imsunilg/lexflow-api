namespace LexFlow.Infrastructure.Settings;

/// <summary>Binds the "ExternalGateways" configuration section — timeouts for the live SMTP/SMS/WhatsApp/payment-gateway calls (PRD Module 15).</summary>
public sealed class ExternalGatewayOptions
{
    public const string SectionName = "ExternalGateways";

    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>Stripe/Razorpay/PayPal base URLs — overridable so tests/sandboxes never need code changes.</summary>
    public string StripeBaseUrl { get; set; } = "https://api.stripe.com";

    public string RazorpayBaseUrl { get; set; } = "https://api.razorpay.com";

    public string PayPalBaseUrl { get; set; } = "https://api-m.sandbox.paypal.com";

    public string TwilioBaseUrl { get; set; } = "https://api.twilio.com";

    public string Msg91BaseUrl { get; set; } = "https://api.msg91.com";

    public string WhatsAppGraphBaseUrl { get; set; } = "https://graph.facebook.com/v19.0";
}
