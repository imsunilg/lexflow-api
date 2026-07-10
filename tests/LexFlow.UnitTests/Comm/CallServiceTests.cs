using FluentAssertions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Comm;
using LexFlow.Infrastructure.Ops;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Comm;

/// <summary>Module 11 calls: follow-up task quick-create, click-to-call config gating, and the consent-before-recording domain rule.</summary>
public sealed class CallServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private static CallService CreateService(LexFlowDbContext db) => new(db, new TaskService(db), new FakeHttpClientFactory(), new GatewayCredentialResolver(db));

    [Fact]
    public async Task LogAsync_creates_a_follow_up_task_and_links_it_when_requested()
    {
        await using var db = CreateContext(nameof(LogAsync_creates_a_follow_up_task_and_links_it_when_requested));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();

        var call = await service.LogAsync(tenantId, actorId: null, new LogCallInput(null, null, null, "Outbound", 300, "Discussed settlement", CreateFollowUpTask: true, "Send settlement draft"), CancellationToken.None);

        call.FollowUpTaskId.Should().NotBeNull();
        (await db.OpsTasks.CountAsync(t => t.Id == call.FollowUpTaskId)).Should().Be(1);
    }

    [Fact]
    public async Task LogAsync_does_not_create_a_task_when_not_requested()
    {
        await using var db = CreateContext(nameof(LogAsync_does_not_create_a_task_when_not_requested));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();

        var call = await service.LogAsync(tenantId, actorId: null, new LogCallInput(null, null, null, "Inbound", 120, "Quick check-in", CreateFollowUpTask: false, null), CancellationToken.None);

        call.FollowUpTaskId.Should().BeNull();
    }

    [Fact]
    public async Task ClickToCallAsync_throws_when_Twilio_Voice_is_not_configured()
    {
        await using var db = CreateContext(nameof(ClickToCallAsync_throws_when_Twilio_Voice_is_not_configured));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();

        var act = () => service.ClickToCallAsync(tenantId, Guid.NewGuid(), new ClickToCallInput(null, null, "+919999999999", ConsentGiven: false), CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "VOICE_NOT_CONFIGURED");
    }

    [Fact]
    public void CallLog_SetRecording_throws_without_consent()
    {
        var call = new CallLog(Guid.NewGuid(), null, null, null, "Outbound", 0, null, null, "voice_twilio", "CA123", consentGiven: false);

        var act = () => call.SetRecording("blob://recordings/ca123.mp3");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CallLog_SetRecording_succeeds_with_consent()
    {
        var call = new CallLog(Guid.NewGuid(), null, null, null, "Outbound", 0, null, null, "voice_twilio", "CA123", consentGiven: true);

        call.SetRecording("blob://recordings/ca123.mp3");

        call.RecordingBlobPath.Should().Be("blob://recordings/ca123.mp3");
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
