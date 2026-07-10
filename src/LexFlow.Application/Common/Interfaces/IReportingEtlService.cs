namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 13 Database: "heavy aggregates from nightly-built star-schema tables ... (refreshed
/// hourly incremental)." Populates rpt.rpt_dim_lawyer/client/practice_area/date and
/// rpt.rpt_fact_billing/time/matters from the OLTP schemas built in DB-2 through DB-9. Runs as an
/// hourly Hangfire recurring job in LexFlow.Workers; upserts only, per-tenant, so it can run
/// incrementally without a truncate-and-reload.
/// </summary>
public interface IReportingEtlService
{
    Task RunIncrementalAsync(CancellationToken cancellationToken = default);
}
