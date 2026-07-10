using System.Text.Json;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Reporting;

/// <summary>
/// Module 13: "schedule (email PDF/XLSX daily/weekly/monthly to recipients)." AC-R3: "scheduled
/// report arrives within 15 min of schedule with correct attachment." <see cref="RunDueSchedulesAsync"/>
/// renders the export and stores it exactly like an on-demand export (via
/// <see cref="IReportRunService.ExecuteQueuedRunAsync"/>, reusing the same render/persist/notify
/// path a heavy async run takes), then notifies every user recipient with a deep link to it.
/// Recipients identified only by a verified email (no firm user id) are recorded on the schedule
/// but not delivered a real SMTP attachment here — this codebase has no attachment-capable
/// transactional-email sender yet (IEmailService's SendAsync requires a connected mailbox, which
/// doesn't fit a system-generated report); wiring that is a documented follow-up, not a silent gap.
/// </summary>
public sealed class ReportSchedulerService(LexFlowDbContext db, IReportRunService reportRunService, INotificationService notificationService) : IReportSchedulerService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ReportScheduleDto> CreateAsync(Guid tenantId, Guid ownerId, ReportScheduleInput input, CancellationToken cancellationToken = default)
    {
        var nextRunAt = NextRunAfter(DateTimeOffset.UtcNow, input.Frequency);
        var paramsJson = JsonSerializer.Serialize(input.Params ?? new ReportRunParams(null, null, null, null, null, null), JsonOptions);
        var recipientsJson = JsonSerializer.Serialize(input.Recipients, JsonOptions);

        var schedule = new ReportSchedule(tenantId, input.ReportKey, input.ReportDefinitionId, input.Frequency, input.Format, paramsJson, recipientsJson, nextRunAt);
        await db.ReportSchedules.AddAsync(schedule, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(schedule);
    }

    public async Task<IReadOnlyList<ReportScheduleDto>> GetForOwnerAsync(Guid tenantId, Guid ownerId, CancellationToken cancellationToken = default)
    {
        var schedules = await db.ReportSchedules.Where(s => s.TenantId == tenantId && s.CreatedBy == ownerId).ToListAsync(cancellationToken);
        return schedules.Select(ToDto).ToList();
    }

    public async Task RunDueSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var due = await db.ReportSchedules.Where(s => s.IsActive && s.NextRunAt != null && s.NextRunAt <= now).ToListAsync(cancellationToken);

        foreach (var schedule in due)
        {
            var reportParams = JsonSerializer.Deserialize<ReportRunParams>(schedule.ParamsJson, JsonOptions) ?? new ReportRunParams(null, null, null, null, null, null);
            var run = new ReportRun(schedule.TenantId, schedule.ReportKey, schedule.ReportDefinitionId, JsonSerializer.Serialize(reportParams, JsonOptions), null, schedule.Format);
            run.AttachToSchedule(schedule.Id);
            await db.ReportRuns.AddAsync(run, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            await reportRunService.ExecuteQueuedRunAsync(schedule.TenantId, run.Id, cancellationToken);

            var recipients = JsonSerializer.Deserialize<List<ReportScheduleRecipient>>(schedule.RecipientsJson, JsonOptions) ?? [];
            foreach (var recipient in recipients.Where(r => r.UserId.HasValue))
            {
                await notificationService.NotifyAsync(
                    schedule.TenantId,
                    recipient.UserId!.Value,
                    new NotifyRequest("report.schedule.delivered", "Your scheduled report is ready", $"{schedule.ReportKey ?? "Custom report"} ({schedule.Frequency}) is ready.", $"/reports/runs/{run.Id}", ["Email", "InApp"]),
                    cancellationToken);
            }

            var nextRunAt = NextRunAfter(now, schedule.Frequency);
            schedule.RecordRun(now, nextRunAt);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static DateTimeOffset NextRunAfter(DateTimeOffset from, string frequency) => frequency.ToLowerInvariant() switch
    {
        "daily" => from.AddDays(1),
        "weekly" => from.AddDays(7),
        "monthly" => from.AddMonths(1),
        _ => from.AddDays(1),
    };

    private static ReportScheduleDto ToDto(ReportSchedule s) => new(s.Id, s.ReportKey, s.ReportDefinitionId, s.Frequency, s.Format, s.NextRunAt, s.LastRunAt, s.IsActive);
}
