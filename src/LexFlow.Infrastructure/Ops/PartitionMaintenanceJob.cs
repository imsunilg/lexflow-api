using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Ops;

/// <summary>
/// §14 Database design: audit.audit_events, ops.reminder_dispatch_log and
/// comm.email_messages are monthly range-partitioned tables. Each schema ships an
/// idempotent helper function for exactly this job to call
/// (lexflow-database Scripts/10_Audit/AuditEvents/004_Functions.sql "audit.fn_ensure_partition",
/// 07_Ops/ReminderDispatchLog/004_Functions.sql "ops.fn_create_reminder_dispatch_log_partition",
/// 08_Comm/EmailMessages/004_Functions.sql "comm.fn_create_email_messages_partition") —
/// each is CREATE TABLE IF NOT EXISTS under the hood, so calling every function every month
/// is safe to re-run. Runs monthly and creates *next* month's partition ahead of time so an
/// insert never lands on a month boundary with no partition to receive it.
/// </summary>
public sealed class PartitionMaintenanceJob(LexFlowDbContext db)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow;
        var nextMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);

        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT audit.fn_ensure_partition({nextMonth}::date)", cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT ops.fn_create_reminder_dispatch_log_partition({nextMonth}::date)", cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT comm.fn_create_email_messages_partition({nextMonth}::date)", cancellationToken);
    }
}
