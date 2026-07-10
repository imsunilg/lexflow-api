using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to ops.notifications (lexflow-database Scripts/07_Ops/Notifications).
/// One row per in-app notification; <see cref="Channels"/> records which other channels
/// (Email/SMS/WhatsApp/Push) were also attempted for this same logical event, per §22.
/// </summary>
public sealed class Notification : AuditableEntity
{
    private Notification()
    {
    }

    public Notification(Guid tenantId, Guid userId, string kind, string title, string? body, string? deepLink, string channelsJson)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        UserId = userId;
        Kind = kind;
        Title = title;
        Body = body;
        DeepLink = deepLink;
        ChannelsJson = channelsJson;
    }

    public Guid UserId { get; private set; }
    public string Kind { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string? Body { get; private set; }
    public string? DeepLink { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    public string ChannelsJson { get; private set; } = "[]";

    public void MarkRead() => ReadAt ??= DateTimeOffset.UtcNow;
}
