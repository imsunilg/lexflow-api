using Hangfire;
using Hangfire.PostgreSql;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure;
using LexFlow.Infrastructure.Dms;
using LexFlow.Infrastructure.Ops;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options => options
        .UseNpgsqlConnection(builder.Configuration.GetConnectionString("LexFlowDatabase"))));

builder.Services.AddHangfireServer(options =>
{
    options.Queues = ["default"];
});

var host = builder.Build();

// Module 7: outbox-pattern dispatcher for Elasticsearch document indexing — polls
// dms.document_index_outbox for Pending rows and publishes them to ES. Standing in for
// a real Service Bus/queue subscriber (see DocumentIndexDispatchService's own doc
// comment for why); a 1-minute cadence keeps AC-DOC1's "searchable ≤ 60s" budget.
RecurringJob.AddOrUpdate<DocumentIndexDispatchService>(
    "document-index-dispatch",
    service => service.DispatchPendingAsync(CancellationToken.None),
    Cron.Minutely());

// §23: outbox-pattern dispatcher for the Workflow Rule engine — polls
// ops.workflow_event_outbox for Pending rows and runs matching ops.workflow_rules.
// Standing in for a real Service Bus/queue subscriber, same simplification as
// DocumentIndexDispatchService above.
RecurringJob.AddOrUpdate<WorkflowRuleEngine>(
    "workflow-rule-dispatch",
    engine => engine.DispatchPendingAsync(CancellationToken.None),
    Cron.Minutely());

// AC-CAL2: hearing/event/deadline/task reminders must fire and be logged on time —
// a 5-minute cadence keeps the 07:00-court-local day-of reminder within a tight window.
RecurringJob.AddOrUpdate<ReminderDispatchService>(
    "reminder-dispatch",
    service => service.DispatchDueAsync(CancellationToken.None),
    "*/5 * * * *");

// §23 default rule #4 ("Task overdue -> assignee nudge; +24h -> manager"): scans for
// newly-overdue tasks and publishes task.overdue through the same workflow event outbox.
RecurringJob.AddOrUpdate<TaskOverdueScanJob>(
    "task-overdue-scan",
    job => job.ScanAsync(CancellationToken.None),
    Cron.Hourly());

// Module 8 User Flow #7: dunning reminder schedule (due-3, due, +7, +15, +30) + escalation.
// A daily cadence is sufficient — dunning steps are day-granular, never intra-day.
RecurringJob.AddOrUpdate<IDunningService>(
    "dunning-run-due-reminders",
    service => service.RunDueRemindersAsync(CancellationToken.None),
    Cron.Daily());

// Module 13 Database: "heavy aggregates from nightly-built star-schema tables ... (refreshed
// hourly incremental)" — populates rpt.rpt_dim_*/rpt_fact_* from the OLTP schemas.
RecurringJob.AddOrUpdate<IReportingEtlService>(
    "reporting-etl-incremental",
    service => service.RunIncrementalAsync(CancellationToken.None),
    Cron.Hourly());

// AC-R3: "scheduled report arrives within 15 min of schedule" — a 5-minute cadence keeps every
// due schedule comfortably inside that window.
RecurringJob.AddOrUpdate<IReportSchedulerService>(
    "reporting-run-due-schedules",
    service => service.RunDueSchedulesAsync(CancellationToken.None),
    "*/5 * * * *");

// DB-12 / 12_MaterializedViews/README.md: "refreshed every 5 min via Hangfire (not pg_cron)".
RecurringJob.AddOrUpdate<MaterializedViewRefreshJob>(
    "materialized-view-refresh",
    job => job.RefreshAsync(CancellationToken.None),
    "*/5 * * * *");

// Module 6: polling fallback for the webhook-driven external calendar sync — catches any
// missed/expired push subscription. Webhooks are the primary path, so this only needs to be
// frequent enough to be a safety net, not a substitute.
RecurringJob.AddOrUpdate<CalendarSyncPollingJob>(
    "calendar-sync-poll",
    job => job.PollAsync(CancellationToken.None),
    "*/15 * * * *");

// §8 G-AC1/AC-CC3, §29 "nightly integrity job failures (page)": AR reconciliation, the
// retroactive G-AC2 reminder-coverage sweep, and the court-case future-hearing invariant.
// 02:00 UTC keeps it clear of the top-of-hour ETL/report jobs above.
RecurringJob.AddOrUpdate<AuditIntegrityCheckJob>(
    "audit-integrity-nightly",
    job => job.RunAsync(CancellationToken.None),
    Cron.Daily(2));

// §14: monthly partition maintenance for audit.audit_events, ops.reminder_dispatch_log and
// comm.email_messages — creates next month's partition ahead of the boundary.
RecurringJob.AddOrUpdate<PartitionMaintenanceJob>(
    "partition-maintenance-monthly",
    job => job.RunAsync(CancellationToken.None),
    Cron.Monthly());

host.Run();
