using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to ops.external_event_links (lexflow-database Scripts/07_Ops/ExternalEventLinks).</summary>
public sealed class ExternalEventLink : AuditableEntity
{
    private ExternalEventLink()
    {
    }

    public ExternalEventLink(Guid tenantId, Guid eventId, Guid externalAccountId, string externalEventId, string? etag)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        EventId = eventId;
        ExternalAccountId = externalAccountId;
        ExternalEventId = externalEventId;
        Etag = etag;
        LastSyncedAt = DateTimeOffset.UtcNow;
    }

    public Guid EventId { get; private set; }
    public Guid ExternalAccountId { get; private set; }
    public string ExternalEventId { get; private set; } = null!;
    public string? Etag { get; private set; }
    public DateTimeOffset? LastSyncedAt { get; private set; }

    public void MarkSynced(string? etag)
    {
        Etag = etag;
        LastSyncedAt = DateTimeOffset.UtcNow;
    }
}
