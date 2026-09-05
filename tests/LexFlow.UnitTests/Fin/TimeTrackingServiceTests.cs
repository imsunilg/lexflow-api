using FluentAssertions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Fin;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Fin;

/// <summary>Module 9: timer start/pause/resume/stop, timesheet CRUD, and the segregation-of-duties approval workflow.</summary>
public sealed class TimeTrackingServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task StartTimerAsync_auto_pauses_any_existing_timer_for_the_same_user()
    {
        await using var db = CreateContext(nameof(StartTimerAsync_auto_pauses_any_existing_timer_for_the_same_user));
        var service = new TimeTrackingService(db);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var matterA = Guid.NewGuid();
        var matterB = Guid.NewGuid();

        await service.StartTimerAsync(tenantId, userId, new StartTimerInput(matterA, null, null), CancellationToken.None);
        var second = await service.StartTimerAsync(tenantId, userId, new StartTimerInput(matterB, null, null), CancellationToken.None);

        second.MatterId.Should().Be(matterB);
        (await db.RunningTimers.CountAsync(t => t.TenantId == tenantId && t.UserId == userId)).Should().Be(1);
    }

    [Fact]
    public async Task StopTimerAsync_creates_a_draft_entry_rounded_up_to_the_nearest_6_minutes()
    {
        await using var db = CreateContext(nameof(StopTimerAsync_creates_a_draft_entry_rounded_up_to_the_nearest_6_minutes));
        var service = new TimeTrackingService(db);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var matterId = Guid.NewGuid();

        var timer = await service.StartTimerAsync(tenantId, userId, new StartTimerInput(matterId, null, null), CancellationToken.None);
        var rawTimer = await db.RunningTimers.SingleAsync(t => t.UserId == userId);
        // Backdate the timer's start so Elapsed() reports a stable, testable duration (7 minutes)
        // instead of racing the real clock inside a fast-running unit test.
        db.Entry(rawTimer).Property("StartedAt").CurrentValue = DateTimeOffset.UtcNow.AddMinutes(-7);
        await db.SaveChangesAsync();

        var entry = await service.StopTimerAsync(tenantId, userId, new StopTimerInput(true, "Reviewed pleadings", null, null, null), CancellationToken.None);

        entry.Status.Should().Be("Draft");
        entry.Source.Should().Be("timer");
        entry.RoundedMin.Should().Be(12); // 7 minutes rounds up to the next 6-minute increment (12)
        (await service.GetCurrentTimerAsync(tenantId, userId, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task StopTimerAsync_throws_when_the_timer_was_started_without_a_matter()
    {
        await using var db = CreateContext(nameof(StopTimerAsync_throws_when_the_timer_was_started_without_a_matter));
        var service = new TimeTrackingService(db);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await service.StartTimerAsync(tenantId, userId, new StartTimerInput(null, null, "matter-042"), CancellationToken.None);

        var act = () => service.StopTimerAsync(tenantId, userId, new StopTimerInput(true, "narrative", null, null, null), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task StopTimerAsync_accepts_a_matter_supplied_at_stop_time_to_classify_a_blank_timer()
    {
        await using var db = CreateContext(nameof(StopTimerAsync_accepts_a_matter_supplied_at_stop_time_to_classify_a_blank_timer));
        var service = new TimeTrackingService(db);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var matterId = Guid.NewGuid();

        await service.StartTimerAsync(tenantId, userId, new StartTimerInput(null, null, null), CancellationToken.None);

        var entry = await service.StopTimerAsync(tenantId, userId, new StopTimerInput(true, "narrative", null, null, matterId), CancellationToken.None);

        entry.MatterId.Should().Be(matterId);
    }

    [Fact]
    public async Task ApproveAsync_rejects_self_approval()
    {
        await using var db = CreateContext(nameof(ApproveAsync_rejects_self_approval));
        var service = new TimeTrackingService(db);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entry = await service.CreateManualEntryAsync(tenantId, userId, new CreateTimeEntryInput(Guid.NewGuid(), null, DateOnly.FromDateTime(DateTime.UtcNow), null, 60, true, "Court appearance", null), CancellationToken.None);
        await service.SubmitAsync(tenantId, [entry.Id], CancellationToken.None);

        var act = () => service.ApproveAsync(tenantId, userId, [entry.Id], null, CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "SELF_APPROVAL_NOT_ALLOWED");
    }

    [Fact]
    public async Task ApproveAsync_resolves_the_firm_default_rate_and_snapshots_the_amount()
    {
        await using var db = CreateContext(nameof(ApproveAsync_resolves_the_firm_default_rate_and_snapshots_the_amount));
        var timeService = new TimeTrackingService(db);
        var rateCardService = new RateCardService(db);
        var tenantId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var matterId = Guid.NewGuid();

        var submitter = new User(tenantId, "junior@firm.test", "Junior Associate");
        await db.Users.AddAsync(submitter);
        await db.SaveChangesAsync();
        var submitterId = submitter.Id;

        var card = await rateCardService.CreateRateCardAsync(tenantId, "Firm Default", null, isDefault: true, CancellationToken.None);
        await rateCardService.UpsertRateCardEntryAsync(tenantId, card.Id, null, submitterId, 6000, "INR", DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);

        var entry = await timeService.CreateManualEntryAsync(tenantId, submitterId, new CreateTimeEntryInput(matterId, null, DateOnly.FromDateTime(DateTime.UtcNow), null, 60, true, "Drafting", null), CancellationToken.None);
        await timeService.SubmitAsync(tenantId, [entry.Id], CancellationToken.None);

        var approved = await timeService.ApproveAsync(tenantId, approverId, [entry.Id], null, CancellationToken.None);

        approved.Single().Status.Should().Be("Approved");
        approved.Single().RateSnapshot.Should().Be(6000);
        approved.Single().AmountSnapshot.Should().Be(6000); // 60 billable minutes at 6000/hr = 6000
    }

    [Fact]
    public async Task UpdateEntryAsync_throws_once_the_entry_has_been_approved()
    {
        await using var db = CreateContext(nameof(UpdateEntryAsync_throws_once_the_entry_has_been_approved));
        var service = new TimeTrackingService(db);
        var tenantId = Guid.NewGuid();
        var submitter = new User(tenantId, "u@firm.test", "U");
        await db.Users.AddAsync(submitter);
        var submitterId = submitter.Id;
        var card = new RateCard(tenantId, "Default", null, true);
        await db.RateCards.AddAsync(card);
        await db.RateCardEntries.AddAsync(new RateCardEntry(tenantId, card.Id, null, submitterId, 5000, "INR", DateOnly.FromDateTime(DateTime.UtcNow)));
        await db.SaveChangesAsync();

        var entry = await service.CreateManualEntryAsync(tenantId, submitterId, new CreateTimeEntryInput(Guid.NewGuid(), null, DateOnly.FromDateTime(DateTime.UtcNow), null, 30, true, "Call", null), CancellationToken.None);
        await service.SubmitAsync(tenantId, [entry.Id], CancellationToken.None);
        await service.ApproveAsync(tenantId, Guid.NewGuid(), [entry.Id], null, CancellationToken.None);

        var act = () => service.UpdateEntryAsync(tenantId, entry.Id, new UpdateTimeEntryInput(Guid.NewGuid(), null, DateOnly.FromDateTime(DateTime.UtcNow), 45, true, "edited", null), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task RejectAsync_transitions_entries_to_rejected_and_they_can_be_edited_and_resubmitted()
    {
        await using var db = CreateContext(nameof(RejectAsync_transitions_entries_to_rejected_and_they_can_be_edited_and_resubmitted));
        var service = new TimeTrackingService(db);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entry = await service.CreateManualEntryAsync(tenantId, userId, new CreateTimeEntryInput(Guid.NewGuid(), null, DateOnly.FromDateTime(DateTime.UtcNow), null, 30, true, "Draft narrative", null), CancellationToken.None);
        await service.SubmitAsync(tenantId, [entry.Id], CancellationToken.None);

        var rejected = await service.RejectAsync(tenantId, Guid.NewGuid(), [entry.Id], "Needs more detail", CancellationToken.None);
        rejected.Single().Status.Should().Be("Rejected");

        var resubmitted = await service.UpdateEntryAsync(tenantId, entry.Id, new UpdateTimeEntryInput(Guid.NewGuid(), null, DateOnly.FromDateTime(DateTime.UtcNow), 30, true, "Draft narrative, revised", null), CancellationToken.None);
        resubmitted.Status.Should().Be("Draft");
    }
}
