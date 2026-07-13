using FluentAssertions;
using LexFlow.Infrastructure.Ops;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LexFlow.IntegrationTests.Staging;

/// <summary>
/// G-AC1/G-AC2/AC-CC3 (§8), run for real against staging's actual Postgres —
/// the exact same <see cref="AuditIntegrityCheckJob"/> LexFlow.Workers runs
/// nightly in production (see tests/LexFlow.IntegrationTests/Ops for the
/// Testcontainers-backed pre-merge version of this same check), not a
/// re-implementation of its query logic that could drift from the real job.
///
/// Running it here writes a real `integrity.violation` audit event if it
/// finds anything wrong — same as it would in production. That's the
/// intended, safe side effect of this job (see its own doc comment); this
/// test does not touch any other table.
/// </summary>
[Trait("Category", "StagingSmoke")]
public sealed class StagingMoneyIntegrityTests
{
    [Fact]
    public async Task Nightly_integrity_job_finds_no_new_violations_against_staging_right_now()
    {
        var config = StagingTestConfig.FromEnvironment();
        await using var db = new LexFlowDbContext(
            new DbContextOptionsBuilder<LexFlowDbContext>().UseNpgsql(config.DbConnectionString).Options);

        var violationCountBefore = await db.AuditEvents.CountAsync(e => e.Action == "integrity.violation");

        var job = new AuditIntegrityCheckJob(db, NullLogger<AuditIntegrityCheckJob>.Instance);
        await job.RunAsync();

        var violationCountAfter = await db.AuditEvents.CountAsync(e => e.Action == "integrity.violation");

        if (violationCountAfter > violationCountBefore)
        {
            var newest = await db.AuditEvents
                .Where(e => e.Action == "integrity.violation")
                .OrderByDescending(e => e.At)
                .Take(violationCountAfter - violationCountBefore)
                .Select(e => e.After)
                .ToListAsync();

            Assert.Fail(
                $"G-AC1/G-AC2/AC-CC3: {violationCountAfter - violationCountBefore} new integrity " +
                $"violation(s) found in staging: {string.Join(" | ", newest)}");
        }

        violationCountAfter.Should().Be(violationCountBefore);
    }
}
