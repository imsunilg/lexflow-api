using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Comm;

/// <summary>
/// Module 11 internal chat. AC-CM5: PostMessageAsync pushes through IChatBroadcaster
/// (the real SignalR /hubs/chat implementation lives in the Api project — see
/// IChatBroadcaster's own doc comment for why) before returning, so a caller awaiting
/// this call has already triggered delivery to online members.
/// <see cref="ChatMessage.Seq"/> is a Postgres IDENTITY column — EF InMemory (used in
/// this codebase's unit tests) doesn't run that generator, so ordering here is by
/// SentAt; production pagination uses the real seq cursor per the table's own design
/// note (WHERE channel_id = ? AND seq &gt; ? ORDER BY seq).
/// </summary>
public sealed class ChatService(LexFlowDbContext db, IChatBroadcaster broadcaster, ITaskService taskService) : IChatService
{
    public async Task<ChatChannelDto> CreateChannelAsync(Guid tenantId, string kind, string? name, Guid? matterId, Guid? teamId, IReadOnlyList<Guid> memberUserIds, CancellationToken cancellationToken = default)
    {
        var channel = new ChatChannel(tenantId, kind, name, matterId, teamId, retentionDays: null);
        await db.ChatChannels.AddAsync(channel, cancellationToken);

        foreach (var userId in memberUserIds.Distinct())
        {
            await db.ChatMembers.AddAsync(new ChatMember(tenantId, channel.Id, userId, "member"), cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(channel);
    }

    public async Task<ChatChannelDto> GetOrCreateMatterChannelAsync(Guid tenantId, Guid matterId, IReadOnlyList<Guid> memberUserIds, CancellationToken cancellationToken = default)
    {
        var existing = await db.ChatChannels.SingleOrDefaultAsync(c => c.TenantId == tenantId && c.Kind == "Matter" && c.MatterId == matterId, cancellationToken);
        if (existing is not null)
        {
            return ToDto(existing);
        }

        return await CreateChannelAsync(tenantId, "Matter", null, matterId, null, memberUserIds, cancellationToken);
    }

    public async Task<IReadOnlyList<ChatChannelDto>> GetChannelsForUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var channelIds = await db.ChatMembers.Where(m => m.TenantId == tenantId && m.UserId == userId).Select(m => m.ChannelId).ToListAsync(cancellationToken);
        var channels = await db.ChatChannels.Where(c => c.TenantId == tenantId && (channelIds.Contains(c.Id) || c.Kind == "Firm")).ToListAsync(cancellationToken);
        return channels.Select(ToDto).ToList();
    }

    public async Task<ChatMessageDto> PostMessageAsync(Guid tenantId, Guid channelId, Guid senderId, string body, CancellationToken cancellationToken = default)
    {
        var channel = await db.ChatChannels.SingleOrDefaultAsync(c => c.TenantId == tenantId && c.Id == channelId, cancellationToken)
            ?? throw new NotFoundException(nameof(ChatChannel), channelId);

        var message = new ChatMessage(tenantId, channel.Id, senderId, body, taskId: null, documentId: null);
        await db.ChatMessages.AddAsync(message, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var dto = ToDto(message);
        await broadcaster.BroadcastMessageAsync(tenantId, channelId, dto, cancellationToken);
        return dto;
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(Guid tenantId, Guid channelId, long? afterSeq, int limit, CancellationToken cancellationToken = default)
    {
        var query = db.ChatMessages.Where(m => m.TenantId == tenantId && m.ChannelId == channelId);
        if (afterSeq.HasValue)
        {
            query = query.Where(m => m.Seq > afterSeq.Value);
        }

        var messages = await query.OrderBy(m => m.SentAt).Take(Math.Clamp(limit, 1, 500)).ToListAsync(cancellationToken);
        return messages.Select(ToDto).ToList();
    }

    public async Task<Guid> ConvertMessageToTaskAsync(Guid tenantId, Guid actorId, Guid messageId, string title, DateTimeOffset? dueAt, CancellationToken cancellationToken = default)
    {
        var message = await db.ChatMessages.SingleOrDefaultAsync(m => m.TenantId == tenantId && m.Id == messageId, cancellationToken)
            ?? throw new NotFoundException(nameof(ChatMessage), messageId);

        var channel = await db.ChatChannels.SingleAsync(c => c.Id == message.ChannelId, cancellationToken);
        var task = await taskService.CreateAsync(tenantId, actorId, new CreateOpsTaskInput(title, message.Body, channel.MatterId, null, actorId, dueAt, "Medium", null), cancellationToken);

        message.SetTaskId(task.Id);
        await db.SaveChangesAsync(cancellationToken);
        return task.Id;
    }

    private static ChatChannelDto ToDto(ChatChannel c) => new(c.Id, c.Kind, c.Name, c.MatterId, c.TeamId, c.RetentionDays);

    private static ChatMessageDto ToDto(ChatMessage m) => new(m.Id, m.ChannelId, m.SenderId, m.Body, m.Seq, m.TaskId, m.DocumentId, m.SentAt);
}
