using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Legal;

/// <summary>
/// Module 5 hearing lifecycle. G-AC2/AC-CC1: <see cref="RecordOutcomeAsync"/> is the single
/// insertion point for "outcome recorded + next hearing created" — every caller (the API
/// controller today; a future CSV import or mobile-offline-sync reconciliation job would call
/// this same method, never write legal.hearings/legal.hearing_outcomes directly) goes through
/// here, so the transaction boundary and the BR-6 deferred-constraint-trigger safety net always
/// apply. See HearingOutcomes/004_Triggers.sql for why the DB-level check is deferred to COMMIT.
/// </summary>
public sealed class HearingService(LexFlowDbContext db, IOpsTaskService opsTaskService, IWorkflowEventPublisher? workflowEvents = null) : IHearingService
{
    private static readonly (int OffsetMinutes, string Label)[] ReminderOffsets =
    [
        (43_200, "30-day"),
        (10_080, "7-day"),
        (1_440, "1-day"),
        (0, "day-of"),
    ];

    public async Task<HearingDto> CreateAsync(Guid tenantId, Guid caseId, CreateHearingInput input, CancellationToken cancellationToken = default)
    {
        await GetCaseOrThrowAsync(tenantId, caseId, cancellationToken);

        if (!input.AllowBackdate && input.Date < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ConflictException("Hearing date must be today or later; requires hearing.backdate to schedule in the past.", "HEARING_BACKDATE_NOT_ALLOWED");
        }

        var hearing = new Hearing(tenantId, caseId, input.Date, input.Time, "Asia/Kolkata", input.Purpose, input.Courtroom, input.AssignedLawyerId);
        await db.Hearings.AddAsync(hearing, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(hearing);
    }

    public async Task<HearingDto?> GetByIdAsync(Guid tenantId, Guid hearingId, CancellationToken cancellationToken = default)
    {
        var hearing = await db.Hearings.SingleOrDefaultAsync(h => h.TenantId == tenantId && h.Id == hearingId, cancellationToken);
        return hearing is null ? null : ToDto(hearing);
    }

    public async Task<IReadOnlyList<HearingDto>> GetByCaseAsync(Guid tenantId, Guid caseId, CancellationToken cancellationToken = default)
    {
        var hearings = await db.Hearings.Where(h => h.TenantId == tenantId && h.CaseId == caseId).OrderBy(h => h.Date).ToListAsync(cancellationToken);
        return hearings.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<HearingDto>> GetCauseListAsync(Guid tenantId, DateOnly date, Guid? courtId, Guid? lawyerId, CancellationToken cancellationToken = default)
    {
        var query = db.Hearings.Where(h => h.TenantId == tenantId && h.Date == date).AsQueryable();

        if (lawyerId.HasValue)
        {
            query = query.Where(h => h.AssignedLawyerId == lawyerId);
        }

        if (courtId.HasValue)
        {
            var caseIdsForCourt = db.CourtCases.Where(c => c.TenantId == tenantId && c.CourtId == courtId).Select(c => c.Id);
            query = query.Where(h => caseIdsForCourt.Contains(h.CaseId));
        }

        var hearings = await query.OrderBy(h => h.Time).ToListAsync(cancellationToken);
        return hearings.Select(ToDto).ToList();
    }

    public async Task<RecordOutcomeResult> RecordOutcomeAsync(Guid tenantId, Guid? actorId, Guid hearingId, RecordOutcomeInput input, CancellationToken cancellationToken = default)
    {
        var hearing = await db.Hearings.SingleOrDefaultAsync(h => h.TenantId == tenantId && h.Id == hearingId, cancellationToken)
            ?? throw new NotFoundException(nameof(Hearing), hearingId);

        // Error Handling: "outcome on future hearing → 400".
        if (hearing.Date > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new Application.Common.Exceptions.ValidationException([new FluentValidation.Results.ValidationFailure("hearingId", "Cannot record an outcome for a hearing scheduled in the future.")]);
        }

        var decisionCount = new[] { input.NextHearingDate.HasValue, input.SineDie, input.Disposed }.Count(x => x);
        if (decisionCount != 1)
        {
            throw new ConflictException("Exactly one of nextHearingDate, sineDie, or disposed must be specified.", "OUTCOME_DECISION_REQUIRED");
        }

        var courtCase = await db.CourtCases.SingleAsync(c => c.Id == hearing.CaseId, cancellationToken);

        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;

        hearing.SetStatus(input.AdjournReason is not null ? "Adjourned" : "Held");

        var outcome = new HearingOutcome(tenantId, hearingId, input.Summary, input.AdjournReason, actorId);
        await db.HearingOutcomes.AddAsync(outcome, cancellationToken);

        Hearing? nextHearing = null;
        var reminderIds = new List<Guid>();

        if (input.NextHearingDate.HasValue)
        {
            nextHearing = new Hearing(tenantId, hearing.CaseId, input.NextHearingDate.Value, input.NextHearingTime, hearing.CourtTz, input.NextHearingPurpose, hearing.Courtroom, hearing.AssignedLawyerId);
            await db.Hearings.AddAsync(nextHearing, cancellationToken);

            // AC-CC1: 30/7/1-day + same-day reminders, created in the SAME transaction as the
            // outcome + next hearing — this is the entire point of G-AC2's "date safety":
            // a next hearing can never exist in the DB without its reminders alongside it.
            foreach (var (offsetMinutes, _) in ReminderOffsets)
            {
                var reminder = new EventReminder(tenantId, "hearing", nextHearing.Id, offsetMinutes, "Email");
                await db.EventReminders.AddAsync(reminder, cancellationToken);
                reminderIds.Add(reminder.Id);
            }
        }
        else if (input.SineDie)
        {
            courtCase.SetStatus("SineDie");

            // Edge case: "hearing adjourned without next date (sine die status → weekly review
            // task auto-created)".
            await opsTaskService.CreateComplianceTaskAsync(
                tenantId, actorId, courtCase.MatterId,
                $"Weekly sine-die review — case {courtCase.CaseNumber}/{courtCase.CaseYear}",
                "Case adjourned sine die; review weekly for a fresh hearing date.",
                DateTimeOffset.UtcNow.AddDays(7), hearing.AssignedLawyerId, cancellationToken);
        }
        else
        {
            courtCase.SetStatus("Disposed");
        }

        var createdOrderIds = new List<Guid>();
        if (input.Orders is { Count: > 0 })
        {
            foreach (var orderInput in input.Orders)
            {
                var order = new CourtOrder(tenantId, hearing.CaseId, hearingId, orderInput.OrderDate, orderInput.Gist, orderInput.ComplianceDue, orderInput.DocumentId);
                await db.CourtOrders.AddAsync(order, cancellationToken);
                createdOrderIds.Add(order.Id);
            }
        }

        Guid? complianceTaskId = null;
        if (input.CreateComplianceTask)
        {
            var complianceDue = input.Orders?.Select(o => o.ComplianceDue).FirstOrDefault(d => d.HasValue);
            complianceTaskId = await opsTaskService.CreateComplianceTaskAsync(
                tenantId, actorId, courtCase.MatterId,
                $"Compliance follow-up — case {courtCase.CaseNumber}/{courtCase.CaseYear}",
                input.Summary,
                complianceDue.HasValue ? complianceDue.Value.ToDateTime(TimeOnly.MinValue) : null,
                hearing.AssignedLawyerId, cancellationToken);
        }

        if (workflowEvents is not null)
        {
            await workflowEvents.PublishAsync(tenantId, "hearing.outcome_recorded", hearingId, new
            {
                entityId = hearingId,
                caseId = hearing.CaseId,
                matterId = courtCase.MatterId,
                ownerId = hearing.AssignedLawyerId,
                sineDie = input.SineDie,
                disposed = input.Disposed,
                hasNextHearing = nextHearing is not null,
            }, cancellationToken);
        }

        // BR-6/AC-CC3: the deferred constraint trigger on legal.hearings/legal.hearing_outcomes
        // evaluates fn_hearing_chain_invariant_holds() here, at COMMIT — after every write above
        // is visible — so a valid "SineDie"/"Disposed"/"next hearing created" outcome always
        // passes and an invalid one (e.g. a bug that forgot to create the next hearing) is
        // rejected atomically, rolling back the whole transaction.
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            await transaction.DisposeAsync();
        }

        return new RecordOutcomeResult(
            ToOutcomeDto(outcome),
            nextHearing is null ? null : ToDto(nextHearing),
            createdOrderIds,
            reminderIds,
            complianceTaskId);
    }

    private async Task<CourtCase> GetCaseOrThrowAsync(Guid tenantId, Guid caseId, CancellationToken cancellationToken)
        => await db.CourtCases.SingleOrDefaultAsync(c => c.TenantId == tenantId && c.Id == caseId, cancellationToken)
           ?? throw new NotFoundException(nameof(CourtCase), caseId);

    private static HearingOutcomeDto ToOutcomeDto(HearingOutcome o) => new(o.Id, o.HearingId, o.Summary, o.AdjournReason, o.RecordedBy, o.RecordedAt);

    private static HearingDto ToDto(Hearing h) => new(h.Id, h.CaseId, h.Date, h.Time, h.CourtTz, h.Purpose, h.Courtroom, h.AssignedLawyerId, h.Status);
}
