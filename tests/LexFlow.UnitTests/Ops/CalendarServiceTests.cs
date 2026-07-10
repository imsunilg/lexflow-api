using FluentAssertions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Ops;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Ops;

/// <summary>
/// Module 6: RRULE occurrence expansion (Ical.Net-backed), recurrence exceptions
/// (AC-CAL3), free/busy, and the ICS token lifecycle. GetIcsFeedAsync itself calls
/// SetTenantIdAsync (a Postgres-only raw-SQL helper, same as every other
/// public/unauthenticated endpoint in this codebase — e.g. DocumentShareLinkService),
/// so — consistent with those — it isn't exercised end-to-end under EF InMemory here.
/// </summary>
public sealed class CalendarServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task GetCalendarAsync_expands_a_weekly_RRULE_event_into_its_occurrences_within_the_window()
    {
        await using var db = CreateContext(nameof(GetCalendarAsync_expands_a_weekly_RRULE_event_into_its_occurrences_within_the_window));
        var service = new CalendarService(db);
        var tenantId = Guid.NewGuid();

        var start = new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.Zero); // a Monday
        await service.CreateEventAsync(tenantId, null, new CreateCalendarEventInput("Meeting", "Weekly standup", start, start.AddHours(1), false, null, null, null, "FREQ=WEEKLY;BYDAY=MO;COUNT=5", null, null), CancellationToken.None);

        var items = await service.GetCalendarAsync(tenantId, start, start.AddMonths(3), types: null, CancellationToken.None);

        items.Should().HaveCount(5);
        items.Should().OnlyContain(i => i.ItemKind == "Event" && i.StartsAt.DayOfWeek == DayOfWeek.Monday);
    }

    [Fact]
    public async Task GetCalendarAsync_skips_an_occurrence_marked_Deleted_in_recurrence_exceptions()
    {
        await using var db = CreateContext(nameof(GetCalendarAsync_skips_an_occurrence_marked_Deleted_in_recurrence_exceptions));
        var service = new CalendarService(db);
        var tenantId = Guid.NewGuid();

        var start = new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.Zero);
        var created = await service.CreateEventAsync(tenantId, null, new CreateCalendarEventInput("Meeting", "Weekly standup", start, start.AddHours(1), false, null, null, null, "FREQ=WEEKLY;BYDAY=MO;COUNT=5", null, null), CancellationToken.None);

        await db.RecurrenceExceptions.AddAsync(new RecurrenceException(tenantId, created.Id, DateOnly.FromDateTime(start.AddDays(7).Date), "Deleted", null, null));
        await db.SaveChangesAsync();

        var items = await service.GetCalendarAsync(tenantId, start, start.AddMonths(3), types: null, CancellationToken.None);

        items.Should().HaveCount(4, "AC-CAL3: 'this occurrence only' deletion removes one occurrence but leaves the series intact");
    }

    [Fact]
    public async Task GetCalendarAsync_applies_an_occurrence_override_from_a_Modified_exception()
    {
        await using var db = CreateContext(nameof(GetCalendarAsync_applies_an_occurrence_override_from_a_Modified_exception));
        var service = new CalendarService(db);
        var tenantId = Guid.NewGuid();

        var start = new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.Zero);
        var created = await service.CreateEventAsync(tenantId, null, new CreateCalendarEventInput("Meeting", "Weekly standup", start, start.AddHours(1), false, null, null, null, "FREQ=WEEKLY;BYDAY=MO;COUNT=5", null, null), CancellationToken.None);

        var overrideStart = start.AddDays(7).AddHours(3); // same occurrence, moved 3h later
        await db.RecurrenceExceptions.AddAsync(new RecurrenceException(tenantId, created.Id, DateOnly.FromDateTime(start.AddDays(7).Date), "Modified", overrideStart, overrideStart.AddHours(1)));
        await db.SaveChangesAsync();

        var items = await service.GetCalendarAsync(tenantId, start, start.AddMonths(3), types: null, CancellationToken.None);

        items.Should().Contain(i => i.StartsAt == overrideStart);
        items.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetCalendarAsync_merges_hearings_deadlines_and_tasks_alongside_native_events()
    {
        await using var db = CreateContext(nameof(GetCalendarAsync_merges_hearings_deadlines_and_tasks_alongside_native_events));
        var tenantId = Guid.NewGuid();
        var lawyerId = Guid.NewGuid();

        var court = new Court(tenantId, "District Court", "Civil", "Mumbai", "Maharashtra", null);
        await db.Courts.AddAsync(court);
        var client = new Client(tenantId, "CL-1", "Individual", "Priya", "Shah", null, null, null, null, null, null, null, null);
        await db.Clients.AddAsync(client);
        var matter = new Matter(tenantId, "MAT-1", "Test matter", client.Id, "Litigation", null, null, null, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        await db.Matters.AddAsync(matter);
        var courtCase = new CourtCase(tenantId, matter.Id, court.Id, "Civil", "CC-1", 2026, null, DateOnly.FromDateTime(DateTime.UtcNow), null, null, null, null);
        await db.CourtCases.AddAsync(courtCase);
        await db.SaveChangesAsync();

        var hearingDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        var hearing = new Hearing(tenantId, courtCase.Id, hearingDate, new TimeOnly(10, 0), "Asia/Kolkata", "Arguments", "1", lawyerId);
        await db.Hearings.AddAsync(hearing);

        await db.MatterImportantDates.AddAsync(new MatterImportantDate(tenantId, matter.Id, "Limitation", "Limitation date", DateTimeOffset.UtcNow.AddDays(5), "{}", "High"));

        var service = new CalendarService(db);
        var taskService = new TaskService(db);
        await taskService.CreateAsync(tenantId, null, new CreateOpsTaskInput("Draft reply", null, matter.Id, null, lawyerId, DateTimeOffset.UtcNow.AddDays(1), "Medium", "Drafting"), CancellationToken.None);

        await db.SaveChangesAsync();

        var items = await service.GetCalendarAsync(tenantId, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(10), types: null, CancellationToken.None);

        items.Should().Contain(i => i.ItemKind == "Hearing" && i.IsLocked);
        items.Should().Contain(i => i.ItemKind == "Deadline");
        items.Should().Contain(i => i.ItemKind == "Task");
    }

    [Fact]
    public async Task AddReminderAsync_rejects_an_offset_outside_0_to_90_days()
    {
        await using var db = CreateContext(nameof(AddReminderAsync_rejects_an_offset_outside_0_to_90_days));
        var service = new CalendarService(db);
        var tenantId = Guid.NewGuid();
        var ev = await service.CreateEventAsync(tenantId, null, new CreateCalendarEventInput("Meeting", "1:1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), false, null, null, null, null, null, null), CancellationToken.None);

        var act = () => service.AddReminderAsync(tenantId, ev.Id, 999_999, "Email", CancellationToken.None);

        await act.Should().ThrowAsync<Application.Common.Exceptions.ValidationException>();
    }

    [Fact]
    public async Task GetFreeBusyAsync_reports_the_organizers_own_events_as_busy_blocks()
    {
        await using var db = CreateContext(nameof(GetFreeBusyAsync_reports_the_organizers_own_events_as_busy_blocks));
        var service = new CalendarService(db);
        var tenantId = Guid.NewGuid();
        var organizerId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow.AddDays(1);
        await service.CreateEventAsync(tenantId, null, new CreateCalendarEventInput("Meeting", "Client call", start, start.AddHours(1), false, null, null, null, null, organizerId, null), CancellationToken.None);

        var result = await service.GetFreeBusyAsync(tenantId, [organizerId], start.AddHours(-1), start.AddHours(2), CancellationToken.None);

        result.BusyByUser[organizerId].Should().ContainSingle(b => b.Source == "Event");
    }

    [Fact]
    public async Task GetOrCreateIcsTokenAsync_is_stable_across_calls_until_revoked()
    {
        await using var db = CreateContext(nameof(GetOrCreateIcsTokenAsync_is_stable_across_calls_until_revoked));
        var service = new CalendarService(db);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var first = await service.GetOrCreateIcsTokenAsync(tenantId, userId, CancellationToken.None);
        var second = await service.GetOrCreateIcsTokenAsync(tenantId, userId, CancellationToken.None);
        first.Should().Be(second);
        first.Should().StartWith($"{tenantId:N}.");

        await service.RevokeIcsTokenAsync(tenantId, userId, CancellationToken.None);
        var afterRevoke = await service.GetOrCreateIcsTokenAsync(tenantId, userId, CancellationToken.None);

        afterRevoke.Should().NotBe(first, "a revoked token must never be reissued");
    }
}
