using System.Net.Http.Json;
using FluentAssertions;

namespace LexFlow.IntegrationTests.Staging;

/// <summary>
/// C-15's other fixtures (CriticalJourneysSmokeTests, CrossTenantFuzzTests,
/// PermissionMatrixIntegrationTests) all host the API in-process via
/// <c>WebApplicationFactory</c> — that only works against code running in this
/// process, never a real remote deployment, so none of them can literally
/// "run against staging" (Build Playbook E-2 / PRD §38). This is the
/// black-box replacement for the part of C-15 that's actually meaningful to
/// re-run against a live deployment: proving the deployed build can
/// authenticate a real user and serve a real authenticated request, i.e.
/// that API + Postgres + Redis are correctly wired together in staging —
/// not just that the pod reports healthy (build-test's own /health check
/// already covers that at a shallower level).
///
/// Deliberately does NOT re-implement the mutating critical journeys
/// (lead→convert→matter→hearing→outcome, WIP→invoice→pay) against staging —
/// the D-18 Playwright suite already exercises those, end to end, through
/// the real UI, against this same staging deployment (see
/// .github/workflows/release.yml). Duplicating that here in a second
/// language/tool against payload shapes this project can't verify without a
/// live backend would drift the moment either suite changes; better to have
/// one owner for "do the mutating journeys work" (D-18) and let this suite
/// own what's unique to it: authenticated API reachability, plus the G-AC1
/// money-integrity invariant (see StagingMoneyIntegrityTests).
///
/// Only runs when explicitly selected: `dotnet test --filter
/// Category=StagingSmoke`. build-test's own `dotnet test` run excludes this
/// category (see api-ci.yml), the same way it already excludes
/// `Testcontainers`-tagged tests from that run.
/// </summary>
[Trait("Category", "StagingSmoke")]
public sealed class StagingApiSmokeTests
{
    [Fact]
    public async Task Seeded_staging_user_can_log_in_and_fetch_their_own_profile()
    {
        var config = StagingTestConfig.FromEnvironment();
        using var client = new HttpClient { BaseAddress = new Uri(config.ApiBaseUrl) };

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            tenantSlug = config.TenantSlug,
            email = config.Email,
            password = config.Password,
        });

        loginResponse.IsSuccessStatusCode.Should().BeTrue(
            $"staging login must succeed for the pre-seeded user ({config.Email}) — " +
            $"got {(int)loginResponse.StatusCode}. Has tools/E2eSeed been run against " +
            "LEXFLOW_STAGING_DB_CONNECTION for this deploy? (see release.yml's " +
            "'Seed staging' step)");

        var loginBody = await loginResponse.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
        loginBody.Should().NotBeNull();
        loginBody!.Data.Should().NotBeNull();
        loginBody.Data!.AccessToken.Should().NotBeNullOrWhiteSpace();
        loginBody.Data.Requires2fa.Should().BeFalse("the seeded staging user has no 2FA enrolled");

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginBody.Data.AccessToken);

        var meResponse = await client.GetAsync("/api/v1/auth/me");
        meResponse.IsSuccessStatusCode.Should().BeTrue(
            $"an access token minted moments ago by staging itself must be accepted by staging's " +
            $"own auth middleware — got {(int)meResponse.StatusCode}.");

        var meBody = await meResponse.Content.ReadFromJsonAsync<ApiEnvelope<MeData>>();
        meBody?.Data?.Email.Should().Be(config.Email);
    }

    private sealed record ApiEnvelope<T>(T? Data);
    private sealed record LoginData(string AccessToken, int ExpiresIn, bool Requires2fa);
    private sealed record MeData(string Id, string Name, string Email);
}
