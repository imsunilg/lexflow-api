using System.Net;
using System.Net.Mail;
using LexFlow.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace LexFlow.Infrastructure.Settings;

/// <summary>Module 15 §4 SMTP "Send Test Email" (PRD). Real SmtpClient call — the interface boundary is what makes this mockable in tests, not a hardcoded fake here.</summary>
public sealed class SmtpTestSender(IOptions<ExternalGatewayOptions> options) : ISmtpTestSender
{
    public async Task<ExternalCallResult> SendTestEmailAsync(SmtpConfig config, string toAddress, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new SmtpClient(config.Host, config.Port)
            {
                EnableSsl = config.TlsMode is "StartTls" or "Ssl" or "Tls",
                Timeout = options.Value.TimeoutSeconds * 1000,
            };

            if (!string.IsNullOrWhiteSpace(config.Username))
            {
                client.Credentials = new NetworkCredential(config.Username, config.Password);
            }

            using var message = new MailMessage(config.FromAddress, toAddress)
            {
                Subject = "LexFlow SMTP test",
                Body = "This is a test email from your LexFlow firm settings (Module 15 §4).",
            };

            if (!string.IsNullOrWhiteSpace(config.FromName))
            {
                message.From = new MailAddress(config.FromAddress, config.FromName);
            }

            await client.SendMailAsync(message, cancellationToken);
            return new ExternalCallResult(true, "Test email sent.");
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException)
        {
            return new ExternalCallResult(false, $"SMTP test failed: {ex.Message}");
        }
    }
}
