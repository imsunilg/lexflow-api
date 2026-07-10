using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Comm;

/// <summary>Module 11: "Unified client Communication tab: reverse-chron across all channels with channel icons, filter chips."</summary>
public sealed class CommTimelineService(LexFlowDbContext db) : ICommTimelineService
{
    private static readonly string[] AllChannels = ["Email", "SMS", "WhatsApp", "Call"];

    public async Task<IReadOnlyList<CommTimelineEntryDto>> GetTimelineAsync(Guid tenantId, Guid? clientId, IReadOnlyCollection<string>? channels, CancellationToken cancellationToken = default)
    {
        var wanted = channels is { Count: > 0 } ? channels : AllChannels;
        var entries = new List<CommTimelineEntryDto>();

        if (wanted.Contains("Email"))
        {
            var emailQuery = db.EmailMessages.Where(m => m.TenantId == tenantId).AsQueryable();
            if (clientId.HasValue)
            {
                emailQuery = emailQuery.Where(m => m.ClientId == clientId.Value);
            }

            var emails = await emailQuery.OrderByDescending(m => m.SentAt).Take(200).ToListAsync(cancellationToken);
            entries.AddRange(emails.Select(m => new CommTimelineEntryDto("Email", m.Id, m.SentAt, m.Direction, m.Subject ?? "(no subject)")));
        }

        if (wanted.Contains("SMS"))
        {
            var smsQuery = db.SmsMessages.Where(m => m.TenantId == tenantId).AsQueryable();
            if (clientId.HasValue)
            {
                smsQuery = smsQuery.Where(m => m.ClientId == clientId.Value);
            }

            var sms = await smsQuery.OrderByDescending(m => m.SentAt).Take(200).ToListAsync(cancellationToken);
            entries.AddRange(sms.Select(m => new CommTimelineEntryDto("SMS", m.Id, m.SentAt, m.Direction, Truncate(m.Body) ?? string.Empty)));
        }

        if (wanted.Contains("WhatsApp"))
        {
            var waQuery = db.WhatsappMessages.Where(m => m.TenantId == tenantId).AsQueryable();
            if (clientId.HasValue)
            {
                waQuery = waQuery.Where(m => m.ClientId == clientId.Value);
            }

            var whatsapp = await waQuery.OrderByDescending(m => m.CreatedAt).Take(200).ToListAsync(cancellationToken);
            entries.AddRange(whatsapp.Select(m => new CommTimelineEntryDto("WhatsApp", m.Id, m.CreatedAt, m.Direction, Truncate(m.Body) ?? string.Empty)));
        }

        if (wanted.Contains("Call"))
        {
            var callQuery = db.CallLogs.Where(c => c.TenantId == tenantId).AsQueryable();
            if (clientId.HasValue)
            {
                callQuery = callQuery.Where(c => c.ClientId == clientId.Value);
            }

            var calls = await callQuery.OrderByDescending(c => c.OccurredAt).Take(200).ToListAsync(cancellationToken);
            entries.AddRange(calls.Select(c => new CommTimelineEntryDto("Call", c.Id, c.OccurredAt, c.Direction, Truncate(c.Summary) ?? $"{c.DurationSec}s call")));
        }

        return entries.OrderByDescending(e => e.At).ToList();
    }

    private static string? Truncate(string? text) => text is { Length: > 140 } ? text[..140] + "…" : text;
}
