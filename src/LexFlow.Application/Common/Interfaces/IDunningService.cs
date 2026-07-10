namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 8 User Flow #7: dunning reminder schedule (due-3, due, +7, +15, +30) + escalation to lawyer at +30 + per-invoice mute. RunDueRemindersAsync is the Hangfire recurring-job entry point.</summary>
public interface IDunningService
{
    /// <summary>For every Sent/PartiallyPaid/Overdue invoice, ensures its dunning_events are scheduled per the tenant's active DunningSchedule and sends any that are now due and unmuted.</summary>
    Task RunDueRemindersAsync(CancellationToken cancellationToken = default);

    Task MuteAsync(Guid tenantId, Guid invoiceId, CancellationToken cancellationToken = default);

    Task<DunningScheduleDto> UpsertScheduleAsync(Guid tenantId, string name, string stepsJson, bool isActive, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DunningScheduleDto>> GetSchedulesAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed record DunningScheduleDto(Guid Id, string Name, string StepsJson, bool IsActive);
