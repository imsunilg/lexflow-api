using DbUp;
using DbUp.Engine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.Elasticsearch;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace LexFlow.IntegrationTests.Smoke;

/// <summary>
/// §37 Testing Strategy: "integration (Testcontainers: Postgres+Redis+ES; every repository &amp;
/// API slice)... E2E ... critical journeys — lead→convert→matter→hearing→outcome;
/// WIP→invoice→pay." This is the literal Postgres+Redis+ES trio the PRD names, hosting the
/// real ASP.NET Core pipeline via WebApplicationFactory so both journeys run as real HTTP
/// requests end to end, not as direct application-service calls.
///
/// The chosen journeys don't themselves touch document indexing or KB search, so Elasticsearch
/// isn't exercised by any assertion here — it's still started and wired into configuration
/// because §37 names it as part of this tier's infrastructure and future tests in this
/// collection (e.g. doc upload→OCR→search, also in §37's journey list but out of scope for
/// this task) will need it already running.
/// </summary>
public sealed class SmokeTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("lexflow_smoke_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7").Build();

    private readonly ElasticsearchContainer _elasticsearch = new ElasticsearchBuilder("docker.elastic.co/elasticsearch/elasticsearch:8.15.0").Build();

    private ApiFactory? _factory;

    public string ConnectionString => _postgres.GetConnectionString();

    public IServiceProvider Services => _factory!.Services;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync(), _elasticsearch.StartAsync());
        ApplyDatabaseSchema();

        _factory = new ApiFactory(ConnectionString, _redis.GetConnectionString(), _elasticsearch.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask(), _elasticsearch.DisposeAsync().AsTask());
    }

    public HttpClient CreateClient() => _factory!.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private void ApplyDatabaseSchema()
    {
        var scriptsRoot = ResolveDatabaseScriptsPath();

        var scripts = Directory.EnumerateFiles(scriptsRoot, "*.sql", SearchOption.AllDirectories)
            .Select(path => new { Path = path, RelativeName = Path.GetRelativePath(scriptsRoot, path).Replace('\\', '/') })
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
                $"Failed to apply the lexflow-database schema to the test container: {result.Error?.Message}", result.Error);
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
            "Could not locate a sibling 'lexflow-database/Scripts' directory — check out lexflow-database next to lexflow-api to run this test.");
    }

    private sealed class ApiFactory(string postgresConnectionString, string redisConnectionString, string elasticsearchUri) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:LexFlowDatabase"] = postgresConnectionString,
                    ["Redis:ConnectionString"] = redisConnectionString,
                    ["Elasticsearch:Uri"] = elasticsearchUri,
                });
            });
        }
    }
}

[CollectionDefinition(nameof(SmokeTestCollection))]
public sealed class SmokeTestCollection : ICollectionFixture<SmokeTestFixture>;
