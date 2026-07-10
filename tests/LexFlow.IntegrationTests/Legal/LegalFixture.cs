using DbUp;
using DbUp.Engine;
using Testcontainers.PostgreSql;

namespace LexFlow.IntegrationTests.Legal;

/// <summary>
/// Spins up a real PostgreSQL 16 container and applies the actual lexflow-database
/// DbUp schema — same rationale as Audit/AuditTrailFixture.cs, kept as a separate
/// fixture/collection so Legal and Audit integration tests can run in parallel
/// without sharing container state. This is what makes G-AC2 a real guarantee rather
/// than an EF-InMemory approximation: the BR-6/AC-CC3 deferred constraint trigger on
/// legal.hearings/legal.hearing_outcomes (see 04_Legal/HearingOutcomes/004_Triggers.sql)
/// only exists in Postgres, not in any in-memory stand-in.
/// </summary>
public sealed class LegalFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("lexflow_legal_test")
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

[CollectionDefinition(nameof(LegalCollection))]
public sealed class LegalCollection : ICollectionFixture<LegalFixture>;
