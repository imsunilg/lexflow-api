namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 17 User Flow #7: secure per-matter threaded messaging with the firm team. Validation: message length &lt;= 10k chars.</summary>
public interface IPortalMessagingService
{
    Task<IReadOnlyList<PortalMessageThreadDto>> GetThreadsAsync(Guid tenantId, Guid clientId, IReadOnlyCollection<Guid>? visibleMatterIds, CancellationToken cancellationToken = default);

    /// <summary>Throws NotFoundException if threadId's matter does not belong to clientId (or is outside the caller's visible-matter subset).</summary>
    Task<IReadOnlyList<PortalMessageDto>> GetMessagesAsync(Guid tenantId, Guid clientId, IReadOnlyCollection<Guid>? visibleMatterIds, Guid threadId, CancellationToken cancellationToken = default);

    Task<PortalMessageDto> PostMessageAsync(Guid tenantId, Guid clientPortalUserId, Guid clientId, IReadOnlyCollection<Guid>? visibleMatterIds, Guid threadId, string body, CancellationToken cancellationToken = default);
}

public sealed record PortalMessageThreadDto(Guid Id, Guid MatterId, string? Subject, DateTimeOffset? LastMessageAt);

public sealed record PortalMessageDto(Guid Id, Guid ThreadId, bool FromPortalUser, string? SenderName, string Body, DateTimeOffset CreatedAt);
