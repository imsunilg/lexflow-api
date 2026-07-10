using System.Text.Json;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Comm;

/// <summary>
/// Module 11 SMS orchestration. AC-CM4: when the tenant's active gateway config has
/// "dltRequired": true (the default — India-compliance mode, PRD: "SMS must use
/// registered DLT template id, hard block otherwise, per setting compliance.dlt=on"),
/// every send must resolve a comm_templates row with a non-null DltTemplateId; a
/// freeform body or a template lacking a DLT id is rejected with 422 before any
/// provider call is attempted.
/// </summary>
public sealed class SmsService(LexFlowDbContext db, IEnumerable<ISmsProvider> providers) : ISmsService
{
    public async Task<SmsMessageDto> SendAsync(Guid tenantId, SendSmsInput input, CancellationToken cancellationToken = default)
    {
        var activeConfig = await db.GatewayConfigs
            .Where(g => g.TenantId == tenantId && g.IsEnabled && (g.Provider == "sms_twilio" || g.Provider == "sms_msg91"))
            .FirstOrDefaultAsync(cancellationToken);
        if (activeConfig is null)
        {
            throw new DomainRuleException("SMS_NOT_CONFIGURED", "No SMS gateway is configured and enabled for this tenant.");
        }

        var dltRequired = true;
        using (var configDoc = JsonDocument.Parse(activeConfig.ConfigJson))
        {
            if (configDoc.RootElement.TryGetProperty("dltRequired", out var dltProp) && dltProp.ValueKind is JsonValueKind.False)
            {
                dltRequired = false;
            }
        }

        string body;
        string? dltTemplateId = null;

        if (input.TemplateId.HasValue)
        {
            var template = await db.CommTemplates.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Id == input.TemplateId.Value && t.Channel == "SMS" && t.IsActive, cancellationToken)
                ?? throw new NotFoundException(nameof(CommTemplate), input.TemplateId.Value);

            body = ApplyVariables(template.Body, input.Variables);
            dltTemplateId = template.DltTemplateId;
        }
        else
        {
            body = input.FreeformBody ?? throw new Application.Common.Exceptions.ValidationException([new FluentValidation.Results.ValidationFailure("templateId", "Either templateId or freeformBody is required.")]);
        }

        if (dltRequired && string.IsNullOrWhiteSpace(dltTemplateId))
        {
            throw new DomainRuleException("DLT_TEMPLATE_REQUIRED", "India-compliance mode requires a DLT-registered template for SMS; this send has none.");
        }

        var provider = providers.SingleOrDefault(p => string.Equals(p.Provider, activeConfig.Provider, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException("SmsProvider", activeConfig.Provider);

        var result = await provider.SendAsync(tenantId, input.ToNumber, body, dltTemplateId, cancellationToken);

        var message = new SmsMessage(tenantId, input.ClientId, input.MatterId, "Outbound", null, input.ToNumber, body, dltTemplateId, activeConfig.Provider);
        message.SetProviderResult(result.ProviderMessageId, result.Status);
        await db.SmsMessages.AddAsync(message, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(message);
    }

    public async Task<IReadOnlyList<SmsMessageDto>> GetForClientAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default)
    {
        var messages = await db.SmsMessages.Where(m => m.TenantId == tenantId && m.ClientId == clientId).OrderByDescending(m => m.SentAt).ToListAsync(cancellationToken);
        return messages.Select(ToDto).ToList();
    }

    public async Task<SmsMessageDto> RecordInboundAsync(Guid tenantId, string fromNumber, string toNumber, string body, string? providerMessageId, string provider, CancellationToken cancellationToken = default)
    {
        Guid? clientId = await db.Clients.Where(c => c.TenantId == tenantId && c.PhoneE164 == fromNumber).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(cancellationToken);

        var message = new SmsMessage(tenantId, clientId, null, "Inbound", fromNumber, toNumber, body, null, provider);
        message.SetProviderResult(providerMessageId, "Delivered");
        await db.SmsMessages.AddAsync(message, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(message);
    }

    public async Task UpdateStatusAsync(Guid tenantId, string providerMessageId, string status, CancellationToken cancellationToken = default)
    {
        var message = await db.SmsMessages.SingleOrDefaultAsync(m => m.TenantId == tenantId && m.ProviderMessageId == providerMessageId, cancellationToken);
        if (message is null)
        {
            return;
        }

        message.SetProviderResult(providerMessageId, status);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string ApplyVariables(string body, IReadOnlyDictionary<string, string>? variables)
    {
        if (variables is null)
        {
            return body;
        }

        foreach (var (key, value) in variables)
        {
            body = body.Replace("{{" + key + "}}", value);
        }

        return body;
    }

    private static SmsMessageDto ToDto(SmsMessage m) => new(m.Id, m.ClientId, m.MatterId, m.Direction, m.FromNumber, m.ToNumber, m.Body, m.DltTemplateId, m.Provider, m.ProviderMessageId, m.Status, m.SentAt);
}
