using FluentAssertions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Comm;
using LexFlow.Infrastructure.Ops;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Comm;

/// <summary>Module 11 internal chat: per-matter auto-channel idempotency, AC-CM5 broadcast-on-post, and message-&gt;task conversion.</summary>
public sealed class ChatServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private static (ChatService Service, FakeChatBroadcaster Broadcaster) CreateService(LexFlowDbContext db)
    {
        var broadcaster = new FakeChatBroadcaster();
        return (new ChatService(db, broadcaster, new TaskService(db)), broadcaster);
    }

    [Fact]
    public async Task GetOrCreateMatterChannelAsync_is_idempotent_for_the_same_matter()
    {
        await using var db = CreateContext(nameof(GetOrCreateMatterChannelAsync_is_idempotent_for_the_same_matter));
        var (service, _) = CreateService(db);
        var tenantId = Guid.NewGuid();
        var matterId = Guid.NewGuid();
        var member = Guid.NewGuid();

        var first = await service.GetOrCreateMatterChannelAsync(tenantId, matterId, [member], CancellationToken.None);
        var second = await service.GetOrCreateMatterChannelAsync(tenantId, matterId, [member], CancellationToken.None);

        second.Id.Should().Be(first.Id);
        (await db.ChatChannels.CountAsync(c => c.TenantId == tenantId && c.MatterId == matterId)).Should().Be(1);
    }

    [Fact]
    public async Task PostMessageAsync_persists_and_broadcasts_the_message()
    {
        await using var db = CreateContext(nameof(PostMessageAsync_persists_and_broadcasts_the_message));
        var (service, broadcaster) = CreateService(db);
        var tenantId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var channel = await service.CreateChannelAsync(tenantId, "Firm", "General", null, null, [senderId], CancellationToken.None);

        var message = await service.PostMessageAsync(tenantId, channel.Id, senderId, "Hello team", CancellationToken.None);

        message.Body.Should().Be("Hello team");
        broadcaster.BroadcastedMessages.Should().ContainSingle(m => m.Id == message.Id);
    }

    [Fact]
    public async Task GetMessagesAsync_returns_posted_messages_for_the_channel()
    {
        await using var db = CreateContext(nameof(GetMessagesAsync_returns_posted_messages_for_the_channel));
        var (service, _) = CreateService(db);
        var tenantId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var channel = await service.CreateChannelAsync(tenantId, "Firm", "General", null, null, [senderId], CancellationToken.None);
        await service.PostMessageAsync(tenantId, channel.Id, senderId, "First", CancellationToken.None);
        await service.PostMessageAsync(tenantId, channel.Id, senderId, "Second", CancellationToken.None);

        var messages = await service.GetMessagesAsync(tenantId, channel.Id, afterSeq: null, limit: 50, CancellationToken.None);

        messages.Should().HaveCount(2);
        messages.Select(m => m.Body).Should().Contain(["First", "Second"]);
    }

    [Fact]
    public async Task ConvertMessageToTaskAsync_creates_a_task_and_links_it_back_to_the_message()
    {
        await using var db = CreateContext(nameof(ConvertMessageToTaskAsync_creates_a_task_and_links_it_back_to_the_message));
        var (service, _) = CreateService(db);
        var tenantId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var channel = await service.CreateChannelAsync(tenantId, "Firm", "General", null, null, [senderId], CancellationToken.None);
        var message = await service.PostMessageAsync(tenantId, channel.Id, senderId, "Please file the reply by Friday", CancellationToken.None);

        var taskId = await service.ConvertMessageToTaskAsync(tenantId, senderId, message.Id, "File reply", DateTimeOffset.UtcNow.AddDays(3), CancellationToken.None);

        (await db.OpsTasks.CountAsync(t => t.Id == taskId)).Should().Be(1);
        (await db.ChatMessages.SingleAsync(m => m.Id == message.Id)).TaskId.Should().Be(taskId);
    }

    private sealed class FakeChatBroadcaster : IChatBroadcaster
    {
        public List<ChatMessageDto> BroadcastedMessages { get; } = [];

        public Task BroadcastMessageAsync(Guid tenantId, Guid channelId, ChatMessageDto message, CancellationToken cancellationToken = default)
        {
            BroadcastedMessages.Add(message);
            return Task.CompletedTask;
        }
    }
}
