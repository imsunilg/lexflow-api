using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Portal;

/// <summary>Module 17 User Flow #7: secure per-matter threaded messaging with the firm team (never email).</summary>
public sealed class PortalMessagingService(LexFlowDbContext db) : IPortalMessagingService
{
    public async Task<IReadOnlyList<PortalMessageThreadDto>> GetThreadsAsync(Guid tenantId, Guid clientId, IReadOnlyCollection<Guid>? visibleMatterIds, CancellationToken cancellationToken = default)
    {
        var matterIds = await db.Matters
            .Where(m => m.TenantId == tenantId && m.ClientId == clientId)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        if (visibleMatterIds is not null)
        {
            matterIds = matterIds.Where(visibleMatterIds.Contains).ToList();
        }

        var threads = await db.PortalMessageThreads
            .Where(t => t.TenantId == tenantId && matterIds.Contains(t.MatterId))
            .OrderByDescending(t => t.LastMessageAt)
            .ToListAsync(cancellationToken);

        return threads.Select(t => new PortalMessageThreadDto(t.Id, t.MatterId, t.Subject, t.LastMessageAt)).ToList();
    }

    public async Task<IReadOnlyList<PortalMessageDto>> GetMessagesAsync(Guid tenantId, Guid clientId, IReadOnlyCollection<Guid>? visibleMatterIds, Guid threadId, CancellationToken cancellationToken = default)
    {
        var thread = await EnsureThreadOwnedByClientAsync(tenantId, clientId, visibleMatterIds, threadId, cancellationToken);

        var messages = await db.PortalMessages
            .Where(m => m.TenantId == tenantId && m.ThreadId == thread.Id)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        var results = new List<PortalMessageDto>();
        foreach (var message in messages)
        {
            string? senderName = null;
            if (message.SenderStaffUserId is { } staffId)
            {
                senderName = await db.Users.Where(u => u.Id == staffId).Select(u => u.Name).SingleOrDefaultAsync(cancellationToken);
            }
            else if (message.SenderClientPortalUserId is { } portalUserId)
            {
                senderName = await db.ClientPortalUsers.Where(u => u.Id == portalUserId).Select(u => u.Name).SingleOrDefaultAsync(cancellationToken);
            }

            results.Add(new PortalMessageDto(message.Id, message.ThreadId, message.SenderClientPortalUserId is not null, senderName, message.Body, message.CreatedAt));
        }

        return results;
    }

    public async Task<PortalMessageDto> PostMessageAsync(Guid tenantId, Guid clientPortalUserId, Guid clientId, IReadOnlyCollection<Guid>? visibleMatterIds, Guid threadId, string body, CancellationToken cancellationToken = default)
    {
        var thread = await EnsureThreadOwnedByClientAsync(tenantId, clientId, visibleMatterIds, threadId, cancellationToken);

        var message = PortalMessage.FromPortalUser(tenantId, thread.Id, clientPortalUserId, body);
        await db.PortalMessages.AddAsync(message, cancellationToken);
        thread.TouchLastMessageAt();
        await db.SaveChangesAsync(cancellationToken);

        var senderName = await db.ClientPortalUsers.Where(u => u.Id == clientPortalUserId).Select(u => u.Name).SingleOrDefaultAsync(cancellationToken);
        return new PortalMessageDto(message.Id, message.ThreadId, true, senderName, message.Body, message.CreatedAt);
    }

    private async Task<PortalMessageThread> EnsureThreadOwnedByClientAsync(Guid tenantId, Guid clientId, IReadOnlyCollection<Guid>? visibleMatterIds, Guid threadId, CancellationToken cancellationToken)
    {
        var thread = await db.PortalMessageThreads.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Id == threadId, cancellationToken)
            ?? throw new NotFoundException(nameof(PortalMessageThread), threadId);

        var matter = await db.Matters.SingleOrDefaultAsync(m => m.TenantId == tenantId && m.Id == thread.MatterId, cancellationToken);
        if (matter is null || matter.ClientId != clientId || (visibleMatterIds is not null && !visibleMatterIds.Contains(matter.Id)))
        {
            throw new NotFoundException(nameof(PortalMessageThread), threadId);
        }

        return thread;
    }
}
