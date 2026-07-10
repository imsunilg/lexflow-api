using FluentAssertions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using LexFlow.Infrastructure.Portal;
using LexFlow.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Portal;

/// <summary>Deterministic in-memory stand-in for JwtTokenService — real RSA signing isn't needed to exercise PortalAuthService's own session/lockout logic.</summary>
internal sealed class FakePortalJwtTokenService : IJwtTokenService
{
    private int _counter;

    public AccessTokenResult IssueAccessToken(Guid userId, Guid tenantId, string role, Guid? branchId, IReadOnlyCollection<string> permissions, string audience = "staff", Guid? clientId = null)
        => new($"access-{userId}-{audience}", 900);

    public (string Token, string Hash) IssueRefreshToken()
    {
        var token = $"refresh-{Guid.NewGuid()}-{_counter++}";
        return (token, HashRefreshToken(token));
    }

    public string HashRefreshToken(string refreshToken) => $"hash:{refreshToken}";

    public string IssuePurposeToken(Guid userId, Guid tenantId, string audience, TimeSpan lifetime) => "purpose-token";

    public PurposeTokenPayload? ValidatePurposeToken(string token, string audience) => null;
}

/// <summary>Module 17: login/refresh/lockout for the portal identity realm, independent of staff AuthService/core.login_history.</summary>
public sealed class PortalAuthServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    /// <summary>core.tenants has no public factory in this codebase — reflection is the established pattern (see TrustServiceTests).</summary>
    private static Tenant CreateTenant(string slug)
    {
        var tenant = (Tenant)Activator.CreateInstance(typeof(Tenant), nonPublic: true)!;
        typeof(Tenant).GetProperty(nameof(Tenant.Name))!.SetValue(tenant, "Test Firm");
        typeof(Tenant).GetProperty(nameof(Tenant.Slug))!.SetValue(tenant, slug);
        typeof(Tenant).GetProperty(nameof(Tenant.Status))!.SetValue(tenant, "Active");
        typeof(Tenant).BaseType!.GetProperty("Id")!.SetValue(tenant, Guid.NewGuid());
        return tenant;
    }

    private static PortalAuthService CreateService(LexFlowDbContext db) =>
        new(db, new Argon2PasswordHasher(), new FakePortalJwtTokenService());

    [Fact]
    public async Task LoginAsync_succeeds_for_a_correct_password_and_returns_a_client_scoped_token()
    {
        await using var db = CreateContext(nameof(LoginAsync_succeeds_for_a_correct_password_and_returns_a_client_scoped_token));
        var tenant = CreateTenant("acme");
        var hasher = new Argon2PasswordHasher();
        var client = new Client(tenant.Id, "C-1", "Individual", "Jane", "Doe", null, "jane@example.com", null, null, null, null, null, null);
        client.SetPortalEnabled(true);
        var portalUser = new ClientPortalUser(tenant.Id, client.Id, "jane@example.com", "Jane Doe");
        portalUser.SetPasswordHash(hasher.Hash("Sup3rSecret!"));
        await db.Tenants.AddAsync(tenant);
        await db.Clients.AddAsync(client);
        await db.ClientPortalUsers.AddAsync(portalUser);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.LoginAsync("acme", "jane@example.com", "Sup3rSecret!", "127.0.0.1", "test-agent");

        result.Outcome.Should().Be(PortalLoginOutcome.Succeeded);
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.User!.ClientId.Should().Be(client.Id);
    }

    [Fact]
    public async Task LoginAsync_fails_for_an_incorrect_password()
    {
        await using var db = CreateContext(nameof(LoginAsync_fails_for_an_incorrect_password));
        var tenant = CreateTenant("acme");
        var hasher = new Argon2PasswordHasher();
        var client = new Client(tenant.Id, "C-1", "Individual", "Jane", "Doe", null, "jane@example.com", null, null, null, null, null, null);
        client.SetPortalEnabled(true);
        var portalUser = new ClientPortalUser(tenant.Id, client.Id, "jane@example.com", "Jane Doe");
        portalUser.SetPasswordHash(hasher.Hash("Sup3rSecret!"));
        await db.Tenants.AddAsync(tenant);
        await db.Clients.AddAsync(client);
        await db.ClientPortalUsers.AddAsync(portalUser);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.LoginAsync("acme", "jane@example.com", "wrong-password", null, null);

        result.Outcome.Should().Be(PortalLoginOutcome.InvalidCredentials);
    }

    [Fact]
    public async Task LoginAsync_locks_out_after_5_failed_attempts_within_15_minutes()
    {
        await using var db = CreateContext(nameof(LoginAsync_locks_out_after_5_failed_attempts_within_15_minutes));
        var tenant = CreateTenant("acme");
        var hasher = new Argon2PasswordHasher();
        var client = new Client(tenant.Id, "C-1", "Individual", "Jane", "Doe", null, "jane@example.com", null, null, null, null, null, null);
        client.SetPortalEnabled(true);
        var portalUser = new ClientPortalUser(tenant.Id, client.Id, "jane@example.com", "Jane Doe");
        portalUser.SetPasswordHash(hasher.Hash("Sup3rSecret!"));
        await db.Tenants.AddAsync(tenant);
        await db.Clients.AddAsync(client);
        await db.ClientPortalUsers.AddAsync(portalUser);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        for (var i = 0; i < 5; i++)
        {
            await service.LoginAsync("acme", "jane@example.com", "wrong-password", null, null);
        }

        var result = await service.LoginAsync("acme", "jane@example.com", "Sup3rSecret!", null, null);

        result.Outcome.Should().Be(PortalLoginOutcome.AccountLocked);
    }

    [Fact]
    public async Task LoginAsync_denies_a_disabled_portal_even_with_the_correct_password()
    {
        await using var db = CreateContext(nameof(LoginAsync_denies_a_disabled_portal_even_with_the_correct_password));
        var tenant = CreateTenant("acme");
        var hasher = new Argon2PasswordHasher();
        var client = new Client(tenant.Id, "C-1", "Individual", "Jane", "Doe", null, "jane@example.com", null, null, null, null, null, null);
        client.SetPortalEnabled(false);
        var portalUser = new ClientPortalUser(tenant.Id, client.Id, "jane@example.com", "Jane Doe");
        portalUser.SetPasswordHash(hasher.Hash("Sup3rSecret!"));
        await db.Tenants.AddAsync(tenant);
        await db.Clients.AddAsync(client);
        await db.ClientPortalUsers.AddAsync(portalUser);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.LoginAsync("acme", "jane@example.com", "Sup3rSecret!", null, null);

        result.Outcome.Should().Be(PortalLoginOutcome.PortalDisabled);
    }

    [Fact]
    public async Task RefreshAsync_rotates_the_session_and_revoking_a_reused_token_kills_the_whole_family()
    {
        await using var db = CreateContext(nameof(RefreshAsync_rotates_the_session_and_revoking_a_reused_token_kills_the_whole_family));
        var tenant = CreateTenant("acme");
        var hasher = new Argon2PasswordHasher();
        var client = new Client(tenant.Id, "C-1", "Individual", "Jane", "Doe", null, "jane@example.com", null, null, null, null, null, null);
        client.SetPortalEnabled(true);
        var portalUser = new ClientPortalUser(tenant.Id, client.Id, "jane@example.com", "Jane Doe");
        portalUser.SetPasswordHash(hasher.Hash("Sup3rSecret!"));
        await db.Tenants.AddAsync(tenant);
        await db.Clients.AddAsync(client);
        await db.ClientPortalUsers.AddAsync(portalUser);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var login = await service.LoginAsync("acme", "jane@example.com", "Sup3rSecret!", null, null);

        var refreshed = await service.RefreshAsync(login.RefreshToken!, null, null);
        refreshed.AccessToken.Should().NotBeNullOrEmpty();

        // Replaying the now-rotated-away original token is theft-presumed: revokes the whole family.
        var act = () => service.RefreshAsync(login.RefreshToken!, null, null);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();

        var act2 = () => service.RefreshAsync(refreshed.RefreshToken, null, null);
        await act2.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
