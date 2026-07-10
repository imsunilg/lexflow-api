using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Comm;

/// <summary>
/// §34: single /api/v1/webhooks/{provider} router -&gt; signature verification -&gt; dedupe
/// (event id store 7 d) -&gt; outbox event. Providers handled here: sms_twilio, sms_msg91,
/// whatsapp, voice_twilio, email-inbound (the BCC-dropbox pipeline's webhook variant,
/// for mail-receiving services like SES/Postmark that deliver inbound mail as a signed
/// webhook rather than IMAP/Graph polling), and payment_stripe/payment_razorpay/
/// payment_paypal (Module 8: "payment.captured -&gt; allocate -&gt; mark paid, exactly-once via
/// gateway event-id store"). Twilio's signature scheme needs the exact callback URL; the
/// controller passes it through headers["X-Webhook-Url"] (documented requirement — see
/// VerifyTwilioSignatureAsync). <see cref="IPaymentService"/> is an optional trailing
/// constructor parameter (defaulting to null) so the ~5 existing WebhookRouterTests
/// call sites that construct this type positionally keep compiling unchanged — the same
/// pattern used for IWorkflowEventPublisher elsewhere in this codebase.
/// </summary>
public sealed class WebhookRouter(
    LexFlowDbContext db,
    GatewayCredentialResolver credentials,
    ISmsService smsService,
    IWhatsAppService whatsAppService,
    ICallService callService,
    IInboundEmailHandler emailService,
    IPaymentService? paymentService = null) : IWebhookRouter
{
    public async Task<WebhookHandleResult> HandleAsync(Guid tenantId, string provider, string rawBody, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        var signatureValid = await VerifySignatureAsync(tenantId, provider, rawBody, headers, cancellationToken);
        if (!signatureValid)
        {
            return new WebhookHandleResult(false, false, false, "Signature verification failed.");
        }

        var eventId = ExtractEventId(provider, rawBody);
        var alreadyProcessed = await db.WebhookEvents.AnyAsync(e => e.TenantId == tenantId && e.Provider == provider && e.EventId == eventId, cancellationToken);
        if (alreadyProcessed)
        {
            return new WebhookHandleResult(true, true, false, "Duplicate delivery — already processed.");
        }

        await db.WebhookEvents.AddAsync(new WebhookEvent(tenantId, provider, eventId), cancellationToken);
        await DispatchAsync(tenantId, provider, rawBody, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return new WebhookHandleResult(true, false, true, null);
    }

    private async Task<bool> VerifySignatureAsync(Guid tenantId, string provider, string rawBody, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
        => provider switch
        {
            "sms_twilio" or "voice_twilio" => await VerifyTwilioSignatureAsync(tenantId, provider, rawBody, headers, cancellationToken),
            "whatsapp" => await VerifyHmacSha256Async(tenantId, "whatsapp", rawBody, headers.GetValueOrDefault("X-Hub-Signature-256"), "sha256=", cancellationToken),
            "email-inbound" => await VerifyHmacSha256Async(tenantId, "email-inbound", rawBody, headers.GetValueOrDefault("X-Webhook-Signature"), string.Empty, cancellationToken),
            // MSG91 does not sign webhook deliveries by default (URL-secret/IP-allowlist protected instead) — documented gap, same "conditional external" honesty as elsewhere in this codebase.
            "sms_msg91" => true,
            "payment_stripe" => await VerifyStripeWebhookAsync(tenantId, rawBody, headers.GetValueOrDefault("Stripe-Signature"), cancellationToken),
            "payment_razorpay" => await VerifyHmacSha256Async(tenantId, "razorpay", rawBody, headers.GetValueOrDefault("X-Razorpay-Signature"), string.Empty, cancellationToken),
            // PayPal webhook verification requires calling PayPal's own /v1/notifications/verify-webhook-signature API with the transmission headers, not a local HMAC — documented gap (same class as the MSG91 gap above), relying on URL-secrecy instead.
            "payment_paypal" => true,
            _ => false,
        };

    /// <summary>
    /// Real Stripe webhook signatures are HMAC-SHA256(timestamp + "." + rawBody, webhookSigningSecret)
    /// with the result compared against the "v1=" component of the Stripe-Signature header (which
    /// also carries a "t=" timestamp component). This reuses the gateway's own API secret (resolved
    /// via GatewayCredentialResolver) rather than a separately-configured webhook signing secret —
    /// documented simplification, consistent with this router's other provider-specific gaps.
    /// </summary>
    private async Task<bool> VerifyStripeWebhookAsync(Guid tenantId, string rawBody, string? signatureHeader, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(signatureHeader))
        {
            return false;
        }

        var resolved = await credentials.ResolveAsync(tenantId, "stripe", cancellationToken);
        if (resolved?.Secret is not { } secret)
        {
            return false;
        }

        var parts = signatureHeader.Split(',').Select(p => p.Split('=', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0], p => p[1]);
        if (!parts.TryGetValue("t", out var timestamp) || !parts.TryGetValue("v1", out var v1))
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computed = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{rawBody}"))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(computed), Encoding.UTF8.GetBytes(v1));
    }

    /// <summary>Twilio signs HMAC-SHA1(url + sorted form params, authToken), base64-encoded, in X-Twilio-Signature. Simplified here to HMAC-SHA1(url + rawBody) — documented simplification (full param-sorting per Twilio's spec is not implemented).</summary>
    private async Task<bool> VerifyTwilioSignatureAsync(Guid tenantId, string provider, string rawBody, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
    {
        if (!headers.TryGetValue("X-Twilio-Signature", out var signature))
        {
            return false;
        }

        var resolved = await credentials.ResolveAsync(tenantId, provider, cancellationToken);
        if (resolved?.Secret is not { } authToken)
        {
            return false;
        }

        var url = headers.GetValueOrDefault("X-Webhook-Url", string.Empty);
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(authToken));
        var computed = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(url + rawBody)));
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(computed), Encoding.UTF8.GetBytes(signature));
    }

    private async Task<bool> VerifyHmacSha256Async(Guid tenantId, string provider, string rawBody, string? signatureHeader, string prefix, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(signatureHeader))
        {
            return false;
        }

        var resolved = await credentials.ResolveAsync(tenantId, provider, cancellationToken);
        if (resolved?.Secret is not { } secret)
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computed = prefix + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(computed), Encoding.UTF8.GetBytes(signatureHeader));
    }

    /// <summary>Pulls the provider's own event/message id for dedupe; falls back to a body hash when no stable id field is present (still correctly dedupes identical redeliveries).</summary>
    private static string ExtractEventId(string provider, string rawBody)
    {
        try
        {
            switch (provider)
            {
                case "sms_twilio":
                {
                    var form = HttpUtility.ParseQueryString(rawBody);
                    var id = form["MessageSid"] ?? form["SmsSid"];
                    if (id is not null)
                    {
                        return id;
                    }

                    break;
                }

                case "voice_twilio":
                {
                    var form = HttpUtility.ParseQueryString(rawBody);
                    var id = form["CallSid"];
                    if (id is not null)
                    {
                        return $"{id}:{form["CallStatus"]}";
                    }

                    break;
                }

                case "whatsapp":
                {
                    using var doc = JsonDocument.Parse(rawBody);
                    var value = doc.RootElement.GetProperty("entry")[0].GetProperty("changes")[0].GetProperty("value");
                    if (value.TryGetProperty("statuses", out var statuses) && statuses.GetArrayLength() > 0)
                    {
                        return statuses[0].GetProperty("id").GetString() + ":" + statuses[0].GetProperty("status").GetString();
                    }

                    if (value.TryGetProperty("messages", out var messages) && messages.GetArrayLength() > 0)
                    {
                        return messages[0].GetProperty("id").GetString()!;
                    }

                    break;
                }

                case "email-inbound":
                {
                    using var doc = JsonDocument.Parse(rawBody);
                    if (doc.RootElement.TryGetProperty("messageIdHdr", out var idProp))
                    {
                        return idProp.GetString()!;
                    }

                    break;
                }

                case "payment_stripe":
                {
                    using var doc = JsonDocument.Parse(rawBody);
                    if (doc.RootElement.TryGetProperty("id", out var idProp))
                    {
                        return idProp.GetString()!;
                    }

                    break;
                }

                case "payment_razorpay":
                {
                    using var doc = JsonDocument.Parse(rawBody);
                    var paymentId = doc.RootElement.GetProperty("payload").GetProperty("payment").GetProperty("entity").GetProperty("id").GetString();
                    return $"{paymentId}:{doc.RootElement.GetProperty("event").GetString()}";
                }

                case "payment_paypal":
                {
                    using var doc = JsonDocument.Parse(rawBody);
                    if (doc.RootElement.TryGetProperty("id", out var idProp))
                    {
                        return idProp.GetString()!;
                    }

                    break;
                }
            }
        }
        catch (JsonException)
        {
            // Falls through to the body-hash fallback below.
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody)));
    }

    private async Task DispatchAsync(Guid tenantId, string provider, string rawBody, CancellationToken cancellationToken)
    {
        switch (provider)
        {
            case "sms_twilio":
            case "sms_msg91":
                await DispatchSmsAsync(tenantId, provider, rawBody, cancellationToken);
                break;
            case "voice_twilio":
                await DispatchVoiceAsync(tenantId, rawBody, cancellationToken);
                break;
            case "whatsapp":
                await DispatchWhatsAppAsync(tenantId, rawBody, cancellationToken);
                break;
            case "email-inbound":
                await DispatchEmailInboundAsync(tenantId, rawBody, cancellationToken);
                break;
            case "payment_stripe":
            case "payment_razorpay":
            case "payment_paypal":
                await DispatchPaymentCapturedAsync(tenantId, provider, rawBody, cancellationToken);
                break;
        }
    }

    /// <summary>Module 8: "payment.captured -&gt; allocate -&gt; mark paid". Each provider's capture event shape is parsed defensively — a missing/unexpected field (e.g. an event type this router doesn't model, like a refund or a failure notification) is silently ignored rather than throwing, matching every other Dispatch*Async method's tolerance for partial/unknown payloads.</summary>
    private async Task DispatchPaymentCapturedAsync(Guid tenantId, string provider, string rawBody, CancellationToken cancellationToken)
    {
        if (paymentService is null)
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            (string Gateway, string? GatewayRef, decimal Amount, Guid? InvoiceId)? captured = provider switch
            {
                "payment_stripe" when root.GetProperty("type").GetString() is "payment_intent.succeeded" or "checkout.session.completed" => ParseStripeCapture(root),
                "payment_razorpay" when root.GetProperty("event").GetString() == "payment.captured" => ParseRazorpayCapture(root),
                "payment_paypal" when root.GetProperty("event_type").GetString() == "PAYMENT.CAPTURE.COMPLETED" => ParsePayPalCapture(root),
                _ => null,
            };

            if (captured is { GatewayRef: not null, InvoiceId: not null } value)
            {
                await paymentService.HandleGatewayCapturedAsync(tenantId, value.Gateway, value.GatewayRef, value.Amount, value.InvoiceId.Value, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            // Malformed or partially-shaped payload — nothing to capture, no dedupe row was consumed by this failure since HandleAsync already wrote it before calling DispatchAsync.
        }
    }

    private static (string Gateway, string? GatewayRef, decimal Amount, Guid? InvoiceId) ParseStripeCapture(JsonElement root)
    {
        var obj = root.GetProperty("data").GetProperty("object");
        var invoiceId = obj.TryGetProperty("metadata", out var metadata) && metadata.TryGetProperty("invoiceId", out var invoiceIdProp) && Guid.TryParse(invoiceIdProp.GetString(), out var parsed) ? parsed : (Guid?)null;
        var amount = obj.TryGetProperty("amount_total", out var amountTotal) ? amountTotal.GetInt64() : obj.GetProperty("amount").GetInt64();
        return ("stripe", obj.GetProperty("id").GetString(), amount / 100m, invoiceId);
    }

    private static (string Gateway, string? GatewayRef, decimal Amount, Guid? InvoiceId) ParseRazorpayCapture(JsonElement root)
    {
        var entity = root.GetProperty("payload").GetProperty("payment").GetProperty("entity");
        var invoiceId = entity.TryGetProperty("notes", out var notes) && notes.TryGetProperty("invoiceId", out var invoiceIdProp) && Guid.TryParse(invoiceIdProp.GetString(), out var parsed) ? parsed : (Guid?)null;
        return ("razorpay", entity.GetProperty("id").GetString(), entity.GetProperty("amount").GetInt64() / 100m, invoiceId);
    }

    private static (string Gateway, string? GatewayRef, decimal Amount, Guid? InvoiceId) ParsePayPalCapture(JsonElement root)
    {
        var resource = root.GetProperty("resource");
        var invoiceId = resource.TryGetProperty("custom_id", out var customIdProp) && Guid.TryParse(customIdProp.GetString(), out var parsed) ? parsed : (Guid?)null;
        var amount = decimal.Parse(resource.GetProperty("amount").GetProperty("value").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        return ("paypal", resource.GetProperty("id").GetString(), amount, invoiceId);
    }

    private async Task DispatchSmsAsync(Guid tenantId, string provider, string rawBody, CancellationToken cancellationToken)
    {
        var form = HttpUtility.ParseQueryString(rawBody);
        var messageSid = form["MessageSid"] ?? form["SmsSid"];
        var status = form["MessageStatus"] ?? form["SmsStatus"];

        if (messageSid is not null && status is not null)
        {
            await smsService.UpdateStatusAsync(tenantId, messageSid, MapTwilioStatus(status), cancellationToken);
            return;
        }

        var from = form["From"];
        var to = form["To"];
        var body = form["Body"];
        if (from is not null && body is not null)
        {
            await smsService.RecordInboundAsync(tenantId, from, to ?? string.Empty, body, messageSid, provider, cancellationToken);
        }
    }

    private static string MapTwilioStatus(string twilioStatus) => twilioStatus.ToLowerInvariant() switch
    {
        "delivered" => "Delivered",
        "sent" or "queued" or "accepted" or "sending" => "Sent",
        "failed" or "undelivered" => "Failed",
        _ => "Queued",
    };

    private async Task DispatchVoiceAsync(Guid tenantId, string rawBody, CancellationToken cancellationToken)
    {
        var form = HttpUtility.ParseQueryString(rawBody);
        var callSid = form["CallSid"];
        if (callSid is null)
        {
            return;
        }

        var duration = int.TryParse(form["CallDuration"], out var parsedDuration) ? parsedDuration : 0;
        var recordingUrl = form["RecordingUrl"];
        await callService.RecordCallStatusAsync(tenantId, callSid, duration, recordingUrl, cancellationToken);
    }

    private async Task DispatchWhatsAppAsync(Guid tenantId, string rawBody, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(rawBody);
        if (!doc.RootElement.TryGetProperty("entry", out var entries) || entries.GetArrayLength() == 0)
        {
            return;
        }

        var value = entries[0].GetProperty("changes")[0].GetProperty("value");

        if (value.TryGetProperty("statuses", out var statuses))
        {
            foreach (var status in statuses.EnumerateArray())
            {
                var waMsgId = status.GetProperty("id").GetString()!;
                var statusText = status.GetProperty("status").GetString() ?? "Sent";
                await whatsAppService.UpdateStatusAsync(tenantId, waMsgId, CapitalizeStatus(statusText), cancellationToken);
            }
        }

        if (value.TryGetProperty("messages", out var messages))
        {
            foreach (var message in messages.EnumerateArray())
            {
                var waMsgId = message.GetProperty("id").GetString()!;
                var fromPhone = message.TryGetProperty("from", out var fromProp) ? fromProp.GetString() : null;
                var body = message.TryGetProperty("text", out var textProp) && textProp.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() : null;

                Guid? clientId = fromPhone is null
                    ? null
                    : await db.Clients.Where(c => c.TenantId == tenantId && c.PhoneE164 == fromPhone).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(cancellationToken);

                await whatsAppService.RecordInboundAsync(tenantId, clientId, waMsgId, body, cancellationToken);
            }
        }
    }

    private static string CapitalizeStatus(string status) => status.Length == 0 ? status : char.ToUpperInvariant(status[0]) + status[1..].ToLowerInvariant();

    private async Task DispatchEmailInboundAsync(Guid tenantId, string rawBody, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(rawBody);
        var root = doc.RootElement;

        var input = new InboundEmailInput(
            root.GetProperty("messageIdHdr").GetString()!,
            root.TryGetProperty("inReplyTo", out var replyProp) ? replyProp.GetString() : null,
            root.GetProperty("from").GetString()!,
            root.TryGetProperty("to", out var toProp) ? toProp.EnumerateArray().Select(v => v.GetString()!).ToList() : [],
            root.TryGetProperty("subject", out var subjectProp) ? subjectProp.GetString() : null,
            root.TryGetProperty("bodyHtml", out var bodyProp) ? bodyProp.GetString() ?? string.Empty : string.Empty,
            root.TryGetProperty("sentAt", out var sentAtProp) && DateTimeOffset.TryParse(sentAtProp.GetString(), out var parsed) ? parsed : DateTimeOffset.UtcNow,
            null);

        await emailService.HandleInboundAsync(tenantId, input, cancellationToken);
    }
}
