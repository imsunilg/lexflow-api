namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 11 internal chat — channels (Firm/Team/Matter/DM), messages, message-&gt;task conversion. Real-time delivery goes through <see cref="IChatBroadcaster"/> (AC-CM5: &lt;500ms to online members).</summary>
public interface IChatService
{
    Task<ChatChannelDto> CreateChannelAsync(Guid tenantId, string kind, string? name, Guid? matterId, Guid? teamId, IReadOnlyList<Guid> memberUserIds, CancellationToken cancellationToken = default);

    /// <summary>Module 11: "per-matter auto-channel" — idempotent, returns the existing channel if one already exists for this matter.</summary>
    Task<ChatChannelDto> GetOrCreateMatterChannelAsync(Guid tenantId, Guid matterId, IReadOnlyList<Guid> memberUserIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatChannelDto>> GetChannelsForUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<ChatMessageDto> PostMessageAsync(Guid tenantId, Guid channelId, Guid senderId, string body, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(Guid tenantId, Guid channelId, long? afterSeq, int limit, CancellationToken cancellationToken = default);

    /// <summary>Module 11: "message -> task conversion."</summary>
    Task<Guid> ConvertMessageToTaskAsync(Guid tenantId, Guid actorId, Guid messageId, string title, DateTimeOffset? dueAt, CancellationToken cancellationToken = default);
}

/// <summary>
/// Abstraction over the SignalR /hubs/chat push so Infrastructure (where IChatService
/// lives) never references the Hub type itself (it lives in the Api project, per this
/// solution's dependency direction — Api -&gt; Infrastructure -&gt; Application -&gt; Domain).
/// The Api project registers the real SignalR-backed implementation.
/// </summary>
public interface IChatBroadcaster
{
    Task BroadcastMessageAsync(Guid tenantId, Guid channelId, ChatMessageDto message, CancellationToken cancellationToken = default);
}

public sealed record ChatChannelDto(Guid Id, string Kind, string? Name, Guid? MatterId, Guid? TeamId, int? RetentionDays);

public sealed record ChatMessageDto(Guid Id, Guid ChannelId, Guid? SenderId, string? Body, long Seq, Guid? TaskId, Guid? DocumentId, DateTimeOffset SentAt);
