using System.Text.Json;
using FluentValidation.Results;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Fin;

/// <summary>
/// Module 9: timers (AC-T1: server-anchored, survives refresh/device-switch — the only state is
/// this fin.running_timers row) and the manual-entry/timesheet/approval workflow. Rounding: "up to
/// nearest 6 min default" (<see cref="RoundingIncrementMinutes"/>).
/// </summary>
public sealed class TimeTrackingService(LexFlowDbContext db) : ITimeTrackingService
{
    private const int RoundingIncrementMinutes = 6;
    private const int AutoStopHours = 24;

    public async Task<RunningTimerDto> StartTimerAsync(Guid tenantId, Guid userId, StartTimerInput input, CancellationToken cancellationToken = default)
    {
        var existing = await db.RunningTimers.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.UserId == userId, cancellationToken);
        if (existing is not null)
        {
            // Module 9 User Flow #1: "starting another auto-pauses first (setting: auto-pause | block)". Auto-pause is the default here.
            existing.Pause(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
        }

        // Single-column-PK (user_id) upsert: if a timer already exists for this user (just paused
        // above), replace it with a fresh running one — the new timer is the one going forward.
        if (existing is not null)
        {
            db.RunningTimers.Remove(existing);
            await db.SaveChangesAsync(cancellationToken);
        }

        var contextJson = JsonSerializer.Serialize(new { input.ContextRef, input.ActivityCodeId });
        var timer = new RunningTimer(tenantId, userId, input.MatterId, contextJson);
        await db.RunningTimers.AddAsync(timer, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(timer);
    }

    public async Task<RunningTimerDto> PauseTimerAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var timer = await GetTimerOrThrowAsync(tenantId, userId, cancellationToken);
        timer.Pause(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(timer);
    }

    public async Task<RunningTimerDto> ResumeTimerAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var timer = await GetTimerOrThrowAsync(tenantId, userId, cancellationToken);
        timer.Resume(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(timer);
    }

    public async Task<TimeEntryDto> StopTimerAsync(Guid tenantId, Guid userId, StopTimerInput input, CancellationToken cancellationToken = default)
    {
        var timer = await GetTimerOrThrowAsync(tenantId, userId, cancellationToken);

        if (timer.MatterId is null)
        {
            throw new ValidationException([new ValidationFailure("matterId", "The timer was started without a matter and must be classified before it can be stopped.")]);
        }

        var now = DateTimeOffset.UtcNow;
        var elapsed = timer.Elapsed(now);
        // Edge case: "timer running 24h+ (auto-stop at 24h, entry flagged for review)".
        var cappedMinutes = Math.Min(elapsed.TotalMinutes, AutoStopHours * 60);
        var durationMin = Math.Max(1, (int)Math.Round(cappedMinutes, MidpointRounding.AwayFromZero));
        var roundedMin = RoundUp(durationMin, RoundingIncrementMinutes);

        var entry = new TimeEntry(tenantId, userId, timer.MatterId.Value, input.ActivityCodeId, DateOnly.FromDateTime(timer.StartedAt.UtcDateTime), timer.StartedAt, durationMin, roundedMin, input.Billable, input.Narrative, input.InternalNote, "timer");
        await db.TimeEntries.AddAsync(entry, cancellationToken);
        db.RunningTimers.Remove(timer);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entry);
    }

    public async Task<RunningTimerDto?> GetCurrentTimerAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var timer = await db.RunningTimers.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.UserId == userId, cancellationToken);
        return timer is null ? null : ToDto(timer);
    }

    public async Task<TimeEntryDto> CreateManualEntryAsync(Guid tenantId, Guid userId, CreateTimeEntryInput input, CancellationToken cancellationToken = default)
    {
        if (input.EntryDate > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ValidationException([new ValidationFailure("entryDate", "Time entries cannot be dated in the future.")]);
        }

        var roundedMin = RoundUp(input.DurationMin, RoundingIncrementMinutes);
        var entry = new TimeEntry(tenantId, userId, input.MatterId, input.ActivityCodeId, input.EntryDate, input.StartedAt, input.DurationMin, roundedMin, input.Billable, input.Narrative, input.InternalNote, "manual");
        await db.TimeEntries.AddAsync(entry, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entry);
    }

    public async Task<TimeEntryDto> UpdateEntryAsync(Guid tenantId, Guid entryId, UpdateTimeEntryInput input, CancellationToken cancellationToken = default)
    {
        var entry = await GetEntryOrThrowAsync(tenantId, entryId, cancellationToken);

        if (entry.Status == "Approved" || entry.Status == "Billed")
        {
            throw new ConflictException("Only Draft, Submitted, or Rejected entries can be edited.", "TIME_ENTRY_NOT_EDITABLE");
        }

        try
        {
            entry.UpdateDraft(input.MatterId, input.ActivityCodeId, input.EntryDate, input.DurationMin, RoundUp(input.DurationMin, RoundingIncrementMinutes), input.Billable, input.Narrative, input.InternalNote);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (FinDbTriggerExceptions.IsTimeEntryBilledError(ex))
        {
            throw new DomainRuleException("TIME_ENTRY_BILLED", "This time entry has been billed and is immutable; correct via credit note + rebill (BR-5).");
        }

        return ToDto(entry);
    }

    public async Task DeleteEntryAsync(Guid tenantId, Guid entryId, CancellationToken cancellationToken = default)
    {
        var entry = await GetEntryOrThrowAsync(tenantId, entryId, cancellationToken);
        if (entry.Status is "Approved" or "Billed")
        {
            throw new ConflictException("Only Draft, Submitted, or Rejected entries can be deleted.", "TIME_ENTRY_NOT_EDITABLE");
        }

        try
        {
            db.TimeEntries.Remove(entry);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (FinDbTriggerExceptions.IsTimeEntryBilledError(ex))
        {
            throw new DomainRuleException("TIME_ENTRY_BILLED", "This time entry has been billed and is immutable; correct via credit note + rebill (BR-5).");
        }
    }

    public async Task<TimeEntryDto?> GetEntryAsync(Guid tenantId, Guid entryId, CancellationToken cancellationToken = default)
    {
        var entry = await db.TimeEntries.SingleOrDefaultAsync(e => e.TenantId == tenantId && e.Id == entryId, cancellationToken);
        return entry is null ? null : ToDto(entry);
    }

    public async Task<IReadOnlyList<TimeEntryDto>> GetEntriesAsync(Guid tenantId, TimeEntryFilter filter, CancellationToken cancellationToken = default)
    {
        var query = db.TimeEntries.Where(e => e.TenantId == tenantId).AsQueryable();

        if (filter.UserId.HasValue)
        {
            query = query.Where(e => e.UserId == filter.UserId);
        }

        if (filter.MatterId.HasValue)
        {
            query = query.Where(e => e.MatterId == filter.MatterId);
        }

        if (filter.Status is not null)
        {
            query = query.Where(e => e.Status == filter.Status);
        }

        if (filter.From.HasValue)
        {
            query = query.Where(e => e.EntryDate >= filter.From);
        }

        if (filter.To.HasValue)
        {
            query = query.Where(e => e.EntryDate <= filter.To);
        }

        var entries = await query.OrderByDescending(e => e.EntryDate).ToListAsync(cancellationToken);
        return entries.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<TimeEntryDto>> SubmitAsync(Guid tenantId, IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        var entries = await db.TimeEntries.Where(e => e.TenantId == tenantId && ids.Contains(e.Id)).ToListAsync(cancellationToken);
        foreach (var entry in entries)
        {
            entry.Submit();
        }

        await db.SaveChangesAsync(cancellationToken);
        return entries.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<TimeEntryDto>> ApproveAsync(Guid tenantId, Guid approverId, IReadOnlyList<Guid> ids, decimal? manualRateOverride, CancellationToken cancellationToken = default)
    {
        var entries = await db.TimeEntries.Where(e => e.TenantId == tenantId && ids.Contains(e.Id)).ToListAsync(cancellationToken);

        foreach (var entry in entries)
        {
            // Security Rules: "time.approve cannot approve own entries (segregation)".
            if (entry.UserId == approverId)
            {
                throw new DomainRuleException("SELF_APPROVAL_NOT_ALLOWED", $"Time entry {entry.Id} cannot be approved by its own submitter (segregation of duties).");
            }
        }

        foreach (var entry in entries)
        {
            var user = await db.Users.SingleAsync(u => u.Id == entry.UserId, cancellationToken);
            var rate = manualRateOverride ?? await ResolveRateInlineAsync(tenantId, entry.MatterId, entry.UserId, user.Designation, cancellationToken);
            var billableMinutes = entry.Billable ? entry.RoundedMin : 0;
            var amount = GstCalculator.Round(rate * billableMinutes / 60m);
            entry.Approve(approverId, rate, amount);
        }

        await db.SaveChangesAsync(cancellationToken);
        return entries.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<TimeEntryDto>> RejectAsync(Guid tenantId, Guid approverId, IReadOnlyList<Guid> ids, string? comment, CancellationToken cancellationToken = default)
    {
        var entries = await db.TimeEntries.Where(e => e.TenantId == tenantId && ids.Contains(e.Id)).ToListAsync(cancellationToken);
        foreach (var entry in entries)
        {
            entry.Reject();
        }

        await db.SaveChangesAsync(cancellationToken);
        return entries.Select(ToDto).ToList();
    }

    private async Task<decimal> ResolveRateInlineAsync(Guid tenantId, Guid matterId, Guid userId, string? role, CancellationToken cancellationToken)
    {
        var rateCardService = new RateCardService(db);
        return await rateCardService.ResolveRateAsync(tenantId, matterId, userId, role, null, cancellationToken);
    }

    private async Task<RunningTimer> GetTimerOrThrowAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        => await db.RunningTimers.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.UserId == userId, cancellationToken)
           ?? throw new NotFoundException(nameof(RunningTimer), userId);

    private async Task<TimeEntry> GetEntryOrThrowAsync(Guid tenantId, Guid entryId, CancellationToken cancellationToken)
        => await db.TimeEntries.SingleOrDefaultAsync(e => e.TenantId == tenantId && e.Id == entryId, cancellationToken)
           ?? throw new NotFoundException(nameof(TimeEntry), entryId);

    private static int RoundUp(int minutes, int increment) => ((minutes + increment - 1) / increment) * increment;

    private static RunningTimerDto ToDto(RunningTimer t) => new(t.UserId, t.MatterId, t.StartedAt, t.IsPaused, t.Elapsed(DateTimeOffset.UtcNow), t.ContextJson);

    private static TimeEntryDto ToDto(TimeEntry e) => new(
        e.Id, e.UserId, e.MatterId, e.ActivityCodeId, e.EntryDate, e.StartedAt, e.DurationMin, e.RoundedMin, e.Billable,
        e.Narrative, e.InternalNote, e.Status, e.RateSnapshot, e.AmountSnapshot, e.InvoiceLineId, e.Source, e.ApprovedBy, e.ApprovedAt);
}
