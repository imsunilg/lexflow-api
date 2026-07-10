using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Ops;

/// <summary>
/// Module 4 Dashboard / DB-12: fin.mv_dashboard_revenue, fin.mv_dashboard_outstanding and
/// legal.mv_case_stats each carry a UNIQUE index specifically so they support
/// REFRESH MATERIALIZED VIEW CONCURRENTLY (no reader-blocking lock) — per
/// lexflow-database Scripts/12_MaterializedViews/README.md: "refreshed every 5 min via
/// Hangfire (not pg_cron)." This is that job.
/// </summary>
public sealed class MaterializedViewRefreshJob(LexFlowDbContext db)
{
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync("REFRESH MATERIALIZED VIEW CONCURRENTLY fin.mv_dashboard_revenue", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("REFRESH MATERIALIZED VIEW CONCURRENTLY fin.mv_dashboard_outstanding", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("REFRESH MATERIALIZED VIEW CONCURRENTLY legal.mv_case_stats", cancellationToken);
    }
}
