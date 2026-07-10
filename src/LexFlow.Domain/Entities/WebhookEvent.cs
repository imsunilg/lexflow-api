using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to core.webhook_events (lexflow-database
/// Scripts/02_Core/WebhookEvents, an additive migration for this build). §34: the
/// single /api/v1/webhooks/{provider} router's dedupe store — a unique
/// (tenant_id, provider, event_id) row per externally-delivered event actually
/// processed; a replayed delivery hits the unique constraint and is a no-op.
/// </summary>
public sealed class WebhookEvent : AuditableEntity
{
    private WebhookEvent()
    {
    }

    public WebhookEvent(Guid tenantId, string provider, string eventId)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Provider = provider;
        EventId = eventId;
        ReceivedAt = DateTimeOffset.UtcNow;
    }

    public string Provider { get; private set; } = null!;
    public string EventId { get; private set; } = null!;
    public DateTimeOffset ReceivedAt { get; private set; }
}
