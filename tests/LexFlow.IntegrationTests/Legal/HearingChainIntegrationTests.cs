using FluentAssertions;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Legal;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.IntegrationTests.Legal;

/// <summary>
/// G-AC2 (date safety, non-negotiable) / AC-CC1: "Recording an outcome with next date
/// creates next hearing + reminders (30/7/1-day + same-day 7 AM) in one transaction."
///
/// This exercises <see cref="HearingService.RecordOutcomeAsync"/> — the single insertion
/// point every hearing-creating code path must funnel through (see that method's own doc
/// comment) — three times in a row against a real Postgres container, standing in for the
/// three code paths PRD Module 5 cares about:
///   1. "API" — a controller-driven RecordOutcomeAsync call (the only one that actually
///      exists as a distinct code path in this build).
///   2. "Import" — a bulk-reconciliation job replaying a scraped/staged hearing outcome
///      (Module 5 Edge Cases: "e-court scrape/import mismatches → staged review queue").
///   3. "Mobile offline-sync" — a queued outcome recorded offline and replayed on
///      reconnect (Module 4/5's mobile-offline story).
/// All three are simulated by calling the exact same service method again on the newly
/// created hearing, because that IS the guarantee: there is no second code path that
/// creates a hearing without going through this transaction, so "every code path" reduces
/// to "this one path, exercised repeatedly." Each call is asserted to leave behind exactly
/// the reminder set AC-CC1 requires, and the real BR-6/AC-CC3 deferred constraint trigger
/// (Postgres-only — this is why this test needs a real container, not EF InMemory) must
/// hold after every commit.
/// </summary>
[Collection(nameof(LegalCollection))]
public sealed class HearingChainIntegrationTests(LegalFixture fixture)
{
    private static readonly int[] ExpectedReminderOffsets = [43_200, 10_080, 1_440, 0];

    [Fact]
    public async Task RecordOutcomeAsync_creates_next_hearing_and_reminders_in_one_transaction_across_repeated_calls()
    {
        var options = new DbContextOptionsBuilder<LexFlowDbContext>().UseNpgsql(fixture.ConnectionString).Options;
        await using var db = new LexFlowDbContext(options);

        var tenantId = Guid.NewGuid();
        var client = new Client(tenantId, "CL-TEST-0001", "Individual", "Test", "Client", null, null, null, null, null, null, null, null);
        var court = new Court(tenantId, "Bombay High Court", "High", "Mumbai", "Maharashtra", null);
        var matter = new Matter(tenantId, "MAT-TEST-0001", "G-AC2 test matter", client.Id, "Litigation", null, null, null, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");

        db.AddRange(client, court, matter);
        await db.SaveChangesAsync();

        var courtCase = new CourtCase(tenantId, matter.Id, court.Id, "WP", "1234", DateTime.UtcNow.Year, null, null, null, null, null, appealOfCaseId: null);
        await db.CourtCases.AddAsync(courtCase);
        await db.SaveChangesAsync();

        var initialHearing = new Hearing(tenantId, courtCase.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), null, "Asia/Kolkata", "First hearing", null, null);
        await db.Hearings.AddAsync(initialHearing);
        await db.SaveChangesAsync();

        var opsTaskService = new OpsTaskService(db);
        var hearingService = new HearingService(db, opsTaskService);

        var currentHearingId = initialHearing.Id;

        // 3 repeated calls simulating API / import-replay / offline-sync-replay — see class doc comment.
        for (var step = 1; step <= 3; step++)
        {
            // Scheduled for "today" so the next loop iteration can immediately record its
            // outcome too (RecordOutcomeAsync rejects outcomes on hearings dated in the future —
            // Error Handling: "outcome on future hearing -> 400" — so every hearing in this
            // chain must be dated today or earlier by the time its own outcome is recorded).
            var nextDate = DateOnly.FromDateTime(DateTime.UtcNow);
            var result = await hearingService.RecordOutcomeAsync(
                tenantId,
                actorId: null,
                currentHearingId,
                new Application.Common.Interfaces.RecordOutcomeInput(
                    Summary: $"Step {step}: matter proceeded, adjourned to next date.",
                    AdjournReason: null,
                    NextHearingDate: nextDate,
                    NextHearingTime: null,
                    NextHearingPurpose: "Further arguments",
                    SineDie: false,
                    Disposed: false,
                    Orders: null,
                    CreateComplianceTask: false),
                CancellationToken.None);

            result.NextHearing.Should().NotBeNull($"step {step} must create the next hearing in the same transaction");
            result.CreatedReminderIds.Should().HaveCount(4, $"step {step} must create the 30/7/1-day + same-day reminder set");

            var reminders = await db.EventReminders
                .Where(r => r.TenantId == tenantId && r.EventRefKind == "hearing" && r.EventRefId == result.NextHearing!.Id)
                .ToListAsync();

            reminders.Should().HaveCount(4);
            reminders.Select(r => r.OffsetMinutes).Should().BeEquivalentTo(ExpectedReminderOffsets);

            // BR-6/AC-CC3: the case must always have a future scheduled hearing at this point —
            // the DB's own deferred constraint trigger already enforced this at COMMIT above;
            // this just re-confirms the same invariant from the test's own vantage point.
            var hasFutureScheduled = await db.Hearings.AnyAsync(h =>
                h.CaseId == courtCase.Id && h.Status == "Scheduled" && h.Date >= DateOnly.FromDateTime(DateTime.UtcNow));
            hasFutureScheduled.Should().BeTrue();

            currentHearingId = result.NextHearing!.Id;
        }
    }
}
