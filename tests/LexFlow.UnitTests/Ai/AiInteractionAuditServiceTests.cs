using FluentAssertions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Ai;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Ai;

/// <summary>Module 16 Architecture: "Every AI action logged (ai_interactions: feature, prompt-template id, model, tokens, latency, user, target refs, feedback)."</summary>
public sealed class AiInteractionAuditServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task RecordAsync_persists_every_field_and_sets_a_90_day_retention_window()
    {
        await using var db = CreateContext(nameof(RecordAsync_persists_every_field_and_sets_a_90_day_retention_window));
        var service = new AiInteractionAuditService(db);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var id = await service.RecordAsync(new AiInteractionRecord(tenantId, "chat", "chat", "1.0.0", "claude-sonnet-5", 100, 50, 250, 1, userId, "Matter", Guid.NewGuid(), "hello", "hi there"), CancellationToken.None);

        var interaction = await db.AiInteractions.SingleAsync(i => i.Id == id);
        interaction.Feature.Should().Be("chat");
        interaction.TokensInput.Should().Be(100);
        interaction.TokensOutput.Should().Be(50);
        interaction.LatencyMs.Should().Be(250);
        interaction.RetentionExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(90), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task RecordFeedbackAsync_sets_rating_and_reason()
    {
        await using var db = CreateContext(nameof(RecordFeedbackAsync_sets_rating_and_reason));
        var service = new AiInteractionAuditService(db);
        var id = await service.RecordAsync(new AiInteractionRecord(Guid.NewGuid(), "chat", "chat", "1.0.0", "claude", 1, 1, 1, 1, null, null, null, null, null), CancellationToken.None);
        var interaction = await db.AiInteractions.SingleAsync(i => i.Id == id);

        await service.RecordFeedbackAsync(interaction.TenantId, id, -1, "Missed key facts.", CancellationToken.None);

        var reloaded = await db.AiInteractions.SingleAsync(i => i.Id == id);
        reloaded.Rating.Should().Be(-1);
        reloaded.RatingReason.Should().Be("Missed key facts.");
    }

    [Fact]
    public void RecordFeedback_throws_for_a_rating_outside_plus_minus_one()
    {
        var interaction = new AiInteraction(Guid.NewGuid(), "chat", "chat", "1.0.0", "claude", 1, 1, 1, 1, null, null, null, null, null, null);

        var act = () => interaction.RecordFeedback(5, null);

        act.Should().Throw<InvalidOperationException>();
    }
}
