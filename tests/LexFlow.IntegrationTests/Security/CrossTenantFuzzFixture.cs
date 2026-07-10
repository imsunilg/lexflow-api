using DbUp;
using DbUp.Engine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace LexFlow.IntegrationTests.Security;

/// <summary>
/// G-AC3 (§8): "Automated cross-tenant test suite (every GET/PUT/DELETE with foreign-tenant
/// ids) returns 404/403 in 100% of endpoints." Unlike every other fixture in this project
/// (which drives application services directly against a Testcontainers Postgres, bypassing
/// HTTP), this one hosts the real ASP.NET Core pipeline via <see cref="WebApplicationFactory{TEntryPoint}"/>
/// — G-AC3 is specifically about what a real HTTP request sees end-to-end (routing, auth
/// middleware, TenantScopingMiddleware/RLS, the controller action itself), so an
/// in-process-services test would not be the same guarantee.
///
/// Postgres + Redis only (no Elasticsearch) — G-AC3's own PRD wording never mentions search,
/// and none of the fuzzed GET/PUT/DELETE routes require it; the full-stack smoke test fixture
/// is the one that stands up all three per §37's literal "Testcontainers: Postgres+Redis+ES."
/// Redis is required even here, though: JwtBearer's OnTokenValidated event
/// (Program.cs) calls IUserDenylistService on every authenticated request, which is
/// Redis-backed.
/// </summary>
public sealed class CrossTenantFuzzFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("lexflow_gac3_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7").Build();

    private ApiFactory? _factory;

    public string ConnectionString => _postgres.GetConnectionString();

    public IServiceProvider Services => _factory!.Services;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());
        ApplyDatabaseSchema();

        _factory = new ApiFactory(ConnectionString, _redis.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask());
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

    private sealed class ApiFactory(string postgresConnectionString, string redisConnectionString) : WebApplicationFactory<Program>
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
                    // Left unset deliberately: JwtSigningKeyProvider generates an ephemeral
                    // RSA-2048 key for the process when Jwt:PrivateKeyPem is empty (see that
                    // type's own doc comment) — tokens minted via this same factory's DI
                    // container validate correctly without any key material configured here.
                });
            });
        }
    }
}

[CollectionDefinition(nameof(CrossTenantFuzzCollection))]
public sealed class CrossTenantFuzzCollection : ICollectionFixture<CrossTenantFuzzFixture>;
