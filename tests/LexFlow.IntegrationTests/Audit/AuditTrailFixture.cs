using DbUp;
using DbUp.Engine;
using Testcontainers.PostgreSql;

namespace LexFlow.IntegrationTests.Audit;

/// <summary>
/// Spins up a real PostgreSQL 16 container and applies the actual lexflow-database
/// DbUp schema to it — not EF migrations, since LexFlowDbContext is Database-First
/// against that schema (see LexFlowDbContext's own doc comment). This proves the
/// audit interceptor against the real audit.audit_events table (partitioning,
/// insert-only triggers, CHECK constraints included), not an in-memory stand-in.
/// Shared across the whole test collection because applying ~394 scripts is the
/// expensive part; the actual audit assertions are cheap once the schema is up.
/// </summary>
public sealed class AuditTrailFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("lexflow_audit_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ApplyDatabaseSchema();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    private void ApplyDatabaseSchema()
    {
        var scriptsRoot = ResolveDatabaseScriptsPath();

        var scripts = Directory.EnumerateFiles(scriptsRoot, "*.sql", SearchOption.AllDirectories)
            .Select(path => new
            {
                Path = path,
                RelativeName = Path.GetRelativePath(scriptsRoot, path).Replace('\\', '/'),
            })
            .OrderBy(x => x.RelativeName, StringComparer.Ordinal)
            .Select(x => new SqlScript(x.RelativeName, File.ReadAllText(x.Path)))
            .ToList();

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(ConnectionString)
            .WithScripts(scripts)
            .JournalToPostgresqlTable("public", "dbup_schema_versions")
            .Build();

        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
        {
            throw new InvalidOperationException(
                $"Failed to apply the lexflow-database schema to the test container: {result.Error?.Message}",
                result.Error);
        }
    }

    /// <summary>
    /// lexflow-database is a sibling repo checked out next to lexflow-api under the
    /// same parent directory — this walks up from the test assembly's own directory
    /// looking for "&lt;parent&gt;/lexflow-database/Scripts". Works for any local dev
    /// layout matching that convention; a CI runner would need an equivalent second
    /// checkout step (out of scope of this test — see the Api's own
    /// .github/workflows for the existing lexflow-database CI, which lives in that
    /// repo, not this one).
    /// </summary>
    private static string ResolveDatabaseScriptsPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 15 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "lexflow-database", "Scripts");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate a sibling 'lexflow-database/Scripts' directory. This integration test applies " +
            "the real DbUp schema from that repo (LexFlowDbContext is Database-First against it) rather than " +
            "generating EF migrations — check out lexflow-database next to lexflow-api to run this test.");
    }
}

[CollectionDefinition(nameof(AuditTrailCollection))]
public sealed class AuditTrailCollection : ICollectionFixture<AuditTrailFixture>;
