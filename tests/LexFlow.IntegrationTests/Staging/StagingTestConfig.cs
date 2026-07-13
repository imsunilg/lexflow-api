namespace LexFlow.IntegrationTests.Staging;

/// <summary>
/// Reads the env vars release.yml sets for the `Category=StagingSmoke` test
/// run. Defaults for tenant/email/password match tools/E2eSeed's own
/// defaults (see lexflow-api/tools/E2eSeed/Program.cs) so this doesn't need
/// its own separate seed convention — release.yml runs that exact tool
/// against LEXFLOW_STAGING_DB_CONNECTION before these tests run.
/// </summary>
public sealed record StagingTestConfig(string ApiBaseUrl, string DbConnectionString, string TenantSlug, string Email, string Password)
{
    public static StagingTestConfig FromEnvironment()
    {
        var apiBaseUrl = Environment.GetEnvironmentVariable("LEXFLOW_STAGING_API_URL");
        var dbConnectionString = Environment.GetEnvironmentVariable("LEXFLOW_STAGING_DB_CONNECTION");

        if (string.IsNullOrWhiteSpace(apiBaseUrl) || string.IsNullOrWhiteSpace(dbConnectionString))
        {
            throw new InvalidOperationException(
                "LEXFLOW_STAGING_API_URL and LEXFLOW_STAGING_DB_CONNECTION must both be set to run " +
                "Category=StagingSmoke tests — these are release.yml-only tests (see api-ci.yml's build-test " +
                "job, which explicitly excludes this category) and have nothing valid to point at otherwise.");
        }

        return new StagingTestConfig(
            apiBaseUrl.TrimEnd('/'),
            dbConnectionString,
            Environment.GetEnvironmentVariable("LEXFLOW_STAGING_E2E_TENANT_SLUG") ?? "lexflow-demo",
            Environment.GetEnvironmentVariable("LEXFLOW_STAGING_E2E_EMAIL") ?? "e2e.lawyer@lexflow-demo.test",
            Environment.GetEnvironmentVariable("LEXFLOW_STAGING_E2E_PASSWORD") ?? "E2eTest!2025");
    }
}
