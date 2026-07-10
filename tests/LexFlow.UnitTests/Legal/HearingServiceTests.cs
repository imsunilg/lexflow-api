using FluentAssertions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Legal;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Legal;

/// <summary>G-AC2/AC-CC1: outcome recording creates the next hearing + reminder set (or SineDie/Disposed) in one call, against EF InMemory.</summary>
public sealed class HearingServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private static async Task<(LexFlowDbContext Db, HearingService Service, FakeOpsTaskService OpsTasks, Guid TenantId, CourtCase Case, Hearing Hearing)> SetupAsync(string dbName)
    {
        var db = CreateContext(dbName);
        var tenantId = Guid.NewGuid();
        var client = new Client(tenantId, "CL-1", "Individual", "A", "B", null, null, null, null, null, null, null, null);
        var court = new Court(tenantId, "High Court", "High", null, null, null);
        var matter = new Matter(tenantId, "MAT-1", "Test matter", client.Id, "Litigation", null, null, null, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        db.AddRange(client, court, matter);
        await db.SaveChangesAsync();

        var courtCase = new CourtCase(tenantId, matter.Id, court.Id, "WP", "1", DateTime.UtcNow.Year, null, null, null, null, null, null);
        await db.CourtCases.AddAsync(courtCase);
        await db.SaveChangesAsync();

        var hearing = new Hearing(tenantId, courtCase.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), null, "Asia/Kolkata", "First", null, null);
        await db.Hearings.AddAsync(hearing);
        await db.SaveChangesAsync();

        var opsTasks = new FakeOpsTaskService();
        var service = new HearingService(db, opsTasks);
        return (db, service, opsTasks, tenantId, courtCase, hearing);
    }

    [Fact]
    public async Task RecordOutcomeAsync_with_a_next_date_creates_the_next_hearing_and_four_reminders()
    {
        var (db, service, _, tenantId, _, hearing) = await SetupAsync(nameof(RecordOutcomeAsync_with_a_next_date_creates_the_next_hearing_and_four_reminders));

        var result = await service.RecordOutcomeAsync(
            tenantId, null, hearing.Id,
            new RecordOutcomeInput("Matter proceeded, adjourned to a new date.", null, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), null, "Arguments", false, false, null, false),
            CancellationToken.None);

        result.NextHearing.Should().NotBeNull();
        result.CreatedReminderIds.Should().HaveCount(4);

        var reminders = await db.EventReminders.Where(r => r.EventRefId == result.NextHearing!.Id).ToListAsync();
        reminders.Should().HaveCount(4);
        reminders.Select(r => r.OffsetMinutes).Should().BeEquivalentTo([43_200, 10_080, 1_440, 0]);

        var reloadedHearing = await db.Hearings.SingleAsync(h => h.Id == hearing.Id);
        reloadedHearing.Status.Should().Be("Held");
    }

    [Fact]
    public async Task RecordOutcomeAsync_rejects_an_outcome_for_a_future_hearing()
    {
        var (db, service, _, tenantId, courtCase, _) = await SetupAsync(nameof(RecordOutcomeAsync_rejects_an_outcome_for_a_future_hearing));

        var futureHearing = new Hearing(tenantId, courtCase.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)), null, "Asia/Kolkata", null, null, null);
        await db.Hearings.AddAsync(futureHearing);
        await db.SaveChangesAsync();

        var act = () => service.RecordOutcomeAsync(tenantId, null, futureHearing.Id, new RecordOutcomeInput("Some summary here.", null, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)), null, null, false, false, null, false), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task RecordOutcomeAsync_requires_exactly_one_of_nextDate_sineDie_or_disposed()
    {
        var (_, service, _, tenantId, _, hearing) = await SetupAsync(nameof(RecordOutcomeAsync_requires_exactly_one_of_nextDate_sineDie_or_disposed));

        var act = () => service.RecordOutcomeAsync(tenantId, null, hearing.Id, new RecordOutcomeInput("Some summary here.", null, null, null, null, true, true, null, false), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().Where(e => e.Code == "OUTCOME_DECISION_REQUIRED");
    }

    [Fact]
    public async Task RecordOutcomeAsync_with_sineDie_sets_case_status_and_creates_a_weekly_review_task()
    {
        var (db, service, opsTasks, tenantId, courtCase, hearing) = await SetupAsync(nameof(RecordOutcomeAsync_with_sineDie_sets_case_status_and_creates_a_weekly_review_task));

        await service.RecordOutcomeAsync(tenantId, null, hearing.Id, new RecordOutcomeInput("Adjourned sine die pending further orders.", null, null, null, null, true, false, null, false), CancellationToken.None);

        var reloadedCase = await db.CourtCases.SingleAsync(c => c.Id == courtCase.Id);
        reloadedCase.Status.Should().Be("SineDie");
        opsTasks.CreatedTasks.Should().ContainSingle();
    }

    [Fact]
    public async Task RecordOutcomeAsync_with_disposed_sets_case_status_disposed()
    {
        var (db, service, _, tenantId, courtCase, hearing) = await SetupAsync(nameof(RecordOutcomeAsync_with_disposed_sets_case_status_disposed));

        await service.RecordOutcomeAsync(tenantId, null, hearing.Id, new RecordOutcomeInput("Case disposed on merits.", null, null, null, null, false, true, null, false), CancellationToken.None);

        var reloadedCase = await db.CourtCases.SingleAsync(c => c.Id == courtCase.Id);
        reloadedCase.Status.Should().Be("Disposed");
    }

    private sealed class FakeOpsTaskService : IOpsTaskService
    {
        public List<(Guid MatterId, string Title)> CreatedTasks { get; } = [];

        public Task<Guid> CreateComplianceTaskAsync(Guid tenantId, Guid? actorId, Guid matterId, string title, string? description, DateTimeOffset? dueAt, Guid? ownerId, CancellationToken cancellationToken = default)
        {
            CreatedTasks.Add((matterId, title));
            return Task.FromResult(Guid.NewGuid());
        }
    }
}
