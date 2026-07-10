using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Comm;

/// <summary>
/// Module 11 WhatsApp orchestration. Template (HSM) sends require an active opt-in
/// record (Module 11: "opt-in tracked per client, mandatory before first template
/// send") — checked before any provider call. Session (free-form) sends require an
/// open 24h window from the client's last inbound message; outside that window the
/// send is rejected with a template-suggestion rather than silently failing at the
/// provider (Module 11 Validation Rules).
/// </summary>
public sealed class WhatsAppService(LexFlowDbContext db, IWhatsAppProvider provider) : IWhatsAppService
{
    public async Task<WhatsappMessageDto> SendAsync(Guid tenantId, SendWhatsAppInput input, CancellationToken cancellationToken = default)
    {
        var client = await db.Clients.SingleOrDefaultAsync(c => c.TenantId == tenantId && c.Id == input.ClientId, cancellationToken)
            ?? throw new NotFoundException(nameof(Client), input.ClientId);
        if (string.IsNullOrWhiteSpace(client.PhoneE164))
        {
            throw new DomainRuleException("CLIENT_PHONE_MISSING", "The client has no phone number on file.");
        }

        WhatsappMessage message;

        if (input.TemplateId.HasValue)
        {
            if (!await HasActiveOptInAsync(tenantId, input.ClientId, cancellationToken))
            {
                throw new DomainRuleException("WHATSAPP_OPT_IN_REQUIRED", "This client has no active WhatsApp opt-in — a template message cannot be sent until they opt in.");
            }

            var template = await db.CommTemplates.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Id == input.TemplateId.Value && t.Channel == "WhatsApp" && t.IsActive, cancellationToken)
                ?? throw new NotFoundException(nameof(CommTemplate), input.TemplateId.Value);
            if (string.IsNullOrWhiteSpace(template.WaHsmName))
            {
                throw new DomainRuleException("WHATSAPP_HSM_NOT_APPROVED", "This template has no approved WhatsApp HSM name configured.");
            }

            var result = await provider.SendTemplateMessageAsync(tenantId, client.PhoneE164, template.WaHsmName, input.Variables ?? new Dictionary<string, string>(), cancellationToken);
            message = new WhatsappMessage(tenantId, input.ClientId, result.WaMsgId ?? Guid.NewGuid().ToString(), "Outbound", template.Id, ApplyVariables(template.Body, input.Variables), mediaDocumentId: null, windowExpiresAt: null);
            message.SetStatus(result.Status);
        }
        else
        {
            var windowOpen = await db.WhatsappMessages
                .Where(m => m.TenantId == tenantId && m.ClientId == input.ClientId && m.Direction == "Inbound")
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => m.WindowExpiresAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (windowOpen is null || windowOpen < DateTimeOffset.UtcNow)
            {
                throw new DomainRuleException("WHATSAPP_SESSION_WINDOW_CLOSED", "The 24-hour session window is closed for this client — send an approved template instead.");
            }

            var sessionText = input.SessionText ?? throw new Application.Common.Exceptions.ValidationException([new FluentValidation.Results.ValidationFailure("sessionText", "sessionText is required for a non-template send.")]);
            var result = await provider.SendSessionMessageAsync(tenantId, client.PhoneE164, sessionText, cancellationToken);
            message = new WhatsappMessage(tenantId, input.ClientId, result.WaMsgId ?? Guid.NewGuid().ToString(), "Outbound", null, sessionText, mediaDocumentId: null, windowExpiresAt: null);
            message.SetStatus(result.Status);
        }

        await db.WhatsappMessages.AddAsync(message, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(message);
    }

    public async Task<IReadOnlyList<WhatsappMessageDto>> GetForClientAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default)
    {
        var messages = await db.WhatsappMessages.Where(m => m.TenantId == tenantId && m.ClientId == clientId).OrderByDescending(m => m.CreatedAt).ToListAsync(cancellationToken);
        return messages.Select(ToDto).ToList();
    }

    public async Task<bool> HasActiveOptInAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default)
        => await db.WhatsappOptins.AnyAsync(o => o.TenantId == tenantId && o.ClientId == clientId && o.OptedOutAt == null, cancellationToken);

    public async Task<WhatsAppOptinDto> OptInAsync(Guid tenantId, Guid clientId, string phoneE164, string? source, CancellationToken cancellationToken = default)
    {
        var optin = new WhatsappOptin(tenantId, clientId, phoneE164, source);
        await db.WhatsappOptins.AddAsync(optin, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(optin);
    }

    public async Task OptOutAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default)
    {
        var activeOptins = await db.WhatsappOptins.Where(o => o.TenantId == tenantId && o.ClientId == clientId && o.OptedOutAt == null).ToListAsync(cancellationToken);
        foreach (var optin in activeOptins)
        {
            optin.OptOut();
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<WhatsappMessageDto> RecordInboundAsync(Guid tenantId, Guid? clientId, string waMsgId, string? body, CancellationToken cancellationToken = default)
    {
        var message = new WhatsappMessage(tenantId, clientId, waMsgId, "Inbound", null, body, mediaDocumentId: null, windowExpiresAt: DateTimeOffset.UtcNow.AddHours(24));
        message.SetStatus("Delivered");
        await db.WhatsappMessages.AddAsync(message, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(message);
    }

    public async Task UpdateStatusAsync(Guid tenantId, string waMsgId, string status, CancellationToken cancellationToken = default)
    {
        var message = await db.WhatsappMessages.SingleOrDefaultAsync(m => m.TenantId == tenantId && m.WaMsgId == waMsgId, cancellationToken);
        if (message is null)
        {
            return;
        }

        message.SetStatus(status);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? ApplyVariables(string body, IReadOnlyDictionary<string, string>? variables)
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

    private static WhatsappMessageDto ToDto(WhatsappMessage m) => new(m.Id, m.ClientId, m.WaMsgId, m.Direction, m.TemplateId, m.Body, m.Status, m.WindowExpiresAt);

    private static WhatsAppOptinDto ToDto(WhatsappOptin o) => new(o.Id, o.ClientId, o.PhoneE164, o.OptedInAt, o.OptedOutAt);
}
