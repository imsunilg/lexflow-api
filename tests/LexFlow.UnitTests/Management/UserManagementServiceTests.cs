using FluentAssertions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Management;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Management;

/// <summary>AC-U1 (deactivated user's active JWTs denylisted) and AC-U3 (last Owner cannot be deactivated by any path), against EF InMemory.</summary>
public sealed class UserManagementServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task DeactivateAsync_blocks_the_last_owner()
    {
        await using var db = CreateContext(nameof(DeactivateAsync_blocks_the_last_owner));
        var tenantId = Guid.NewGuid();

        var ownerRole = new Role(tenantId, "owner", "Owner", isSystem: true);
        var owner = new User(tenantId, "owner@example.com", "Owner User");
        db.AddRange(ownerRole, owner);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole(tenantId, owner.Id, ownerRole.Id));
        await db.SaveChangesAsync();

        var service = new UserManagementService(db, new FakeJwtTokenService(), new FakeDenylistService());

        var act = () => service.DeactivateAsync(tenantId, owner.Id, []);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task DeactivateAsync_allows_a_non_last_owner_and_denylists_the_user()
    {
        await using var db = CreateContext(nameof(DeactivateAsync_allows_a_non_last_owner_and_denylists_the_user));
        var tenantId = Guid.NewGuid();

        var ownerRole = new Role(tenantId, "owner", "Owner", isSystem: true);
        var owner1 = new User(tenantId, "owner1@example.com", "Owner One");
        var owner2 = new User(tenantId, "owner2@example.com", "Owner Two");
        db.AddRange(ownerRole, owner1, owner2);
        await db.SaveChangesAsync();
        db.UserRoles.AddRange(new UserRole(tenantId, owner1.Id, ownerRole.Id), new UserRole(tenantId, owner2.Id, ownerRole.Id));
        await db.SaveChangesAsync();

        var denylist = new FakeDenylistService();
        var service = new UserManagementService(db, new FakeJwtTokenService(), denylist);

        await service.DeactivateAsync(tenantId, owner1.Id, []);

        var reloaded = await db.Users.SingleAsync(u => u.Id == owner1.Id);
        reloaded.Status.Should().Be(Domain.Enums.UserStatus.Deactivated);
        denylist.DenylistedUserIds.Should().Contain(owner1.Id);
    }

    [Fact]
    public async Task DeactivateAsync_blocks_on_unresolved_team_lead_assignment_until_reassigned()
    {
        await using var db = CreateContext(nameof(DeactivateAsync_blocks_on_unresolved_team_lead_assignment_until_reassigned));
        var tenantId = Guid.NewGuid();

        var lawyerRole = new Role(tenantId, "lawyer", "Lawyer");
        var lead = new User(tenantId, "lead@example.com", "Team Lead");
        var replacement = new User(tenantId, "replacement@example.com", "Replacement Lead");
        var team = new Team(tenantId, "Litigation Team", lead.Id);
        db.AddRange(lawyerRole, lead, replacement, team);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole(tenantId, lead.Id, lawyerRole.Id));
        await db.SaveChangesAsync();

        var service = new UserManagementService(db, new FakeJwtTokenService(), new FakeDenylistService());

        var blocked = () => service.DeactivateAsync(tenantId, lead.Id, []);
        await blocked.Should().ThrowAsync<ConflictException>();

        await service.DeactivateAsync(tenantId, lead.Id, [new ReassignmentEntry("team_lead", team.Id, replacement.Id)]);

        var reloadedTeam = await db.Teams.SingleAsync(t => t.Id == team.Id);
        reloadedTeam.LeadUserId.Should().Be(replacement.Id);
    }

    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        public AccessTokenResult IssueAccessToken(Guid userId, Guid tenantId, string role, Guid? branchId, IReadOnlyCollection<string> permissions, string audience = "staff", Guid? clientId = null)
            => new("fake-token", 900);

        public (string Token, string Hash) IssueRefreshToken() => ("token", "hash");

        public string HashRefreshToken(string refreshToken) => "hash";

        public string IssuePurposeToken(Guid userId, Guid tenantId, string audience, TimeSpan lifetime) => "purpose-token";

        public PurposeTokenPayload? ValidatePurposeToken(string token, string audience) => null;
    }

    private sealed class FakeDenylistService : IUserDenylistService
    {
        public HashSet<Guid> DenylistedUserIds { get; } = [];

        public Task DenylistAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            DenylistedUserIds.Add(userId);
            return Task.CompletedTask;
        }

        public Task<bool> IsDenylistedAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(DenylistedUserIds.Contains(userId));
    }
}
