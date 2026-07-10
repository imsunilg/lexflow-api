using DbUp;
using DbUp.Engine;
using Testcontainers.PostgreSql;

namespace LexFlow.IntegrationTests.Ops;

/// <summary>
/// Spins up a real PostgreSQL 16 container and applies the actual lexflow-database DbUp
/// schema — same pattern as Legal/LegalFixture.cs and Audit/AuditTrailFixture.cs, kept as its
/// own fixture/collection so this suite can run in parallel with the others. G-AC1
/// ("Nightly integrity job asserts [money reconciliation]; violation pages on-call") is
/// specifically about a job that runs against the real Postgres schema (decimal precision,
/// real FK/constraint behavior) — an EF-InMemory version would not be the same guarantee.
/// </summary>
public sealed class MoneyIntegrityFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("lexflow_money_integrity_test")
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

[CollectionDefinition(nameof(MoneyIntegrityCollection))]
public sealed class MoneyIntegrityCollection : ICollectionFixture<MoneyIntegrityFixture>;
