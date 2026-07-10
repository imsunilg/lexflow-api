using FluentAssertions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using LexFlow.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Reporting;

/// <summary>Module 13 Security: row-level scope predicate resolution — own &lt; team &lt; branch &lt; all.</summary>
public sealed class ReportScopeServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task ResolveAsync_returns_own_scope_when_no_higher_grant_exists()
    {
        await using var db = CreateContext(nameof(ResolveAsync_returns_own_scope_when_no_higher_grant_exists));
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var permissions = new FakePermissionService([new EffectivePermission("reports.operational.own", "reports", "operational", "own")]);
        var service = new ReportScopeService(db, permissions);

        var scope = await service.ResolveAsync(tenantId, userId, "operational", CancellationToken.None);

        scope.Kind.Should().Be("own");
        scope.LawyerKeys.Should().ContainSingle(id => id == userId);
    }

    [Fact]
    public async Task ResolveAsync_returns_none_when_the_caller_has_no_grant_for_the_permission_area()
    {
        await using var db = CreateContext(nameof(ResolveAsync_returns_none_when_the_caller_has_no_grant_for_the_permission_area));
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var permissions = new FakePermissionService([new EffectivePermission("reports.operational.own", "reports", "operational", "own")]);
        var service = new ReportScopeService(db, permissions);

        var scope = await service.ResolveAsync(tenantId, userId, "financial", CancellationToken.None);

        scope.IsDenied.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveAsync_team_scope_includes_every_fellow_member_across_every_team_the_caller_belongs_to()
    {
        await using var db = CreateContext(nameof(ResolveAsync_team_scope_includes_every_fellow_member_across_every_team_the_caller_belongs_to));
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var teammateId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();

        var team = new Team(tenantId, "Banking Litigation Team");
        await db.Teams.AddAsync(team);
        await db.TeamMembers.AddAsync(new TeamMember(tenantId, team.Id, userId));
        await db.TeamMembers.AddAsync(new TeamMember(tenantId, team.Id, teammateId));
        await db.SaveChangesAsync();

        var permissions = new FakePermissionService([new EffectivePermission("reports.operational.team", "reports", "operational", "team")]);
        var service = new ReportScopeService(db, permissions);

        var scope = await service.ResolveAsync(tenantId, userId, "operational", CancellationToken.None);

        scope.Kind.Should().Be("team");
        scope.LawyerKeys.Should().Contain([userId, teammateId]);
        scope.LawyerKeys.Should().NotContain(strangerId);
    }

    [Fact]
    public async Task ResolveAsync_branch_scope_resolves_the_callers_home_branch()
    {
        await using var db = CreateContext(nameof(ResolveAsync_branch_scope_resolves_the_callers_home_branch));
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var user = new User(tenantId, "partner@firm.test", "Partner", branchId);
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();

        var permissions = new FakePermissionService([new EffectivePermission("reports.financial.branch", "reports", "financial", "branch")]);
        var service = new ReportScopeService(db, permissions);

        var scope = await service.ResolveAsync(tenantId, user.Id, "financial", CancellationToken.None);

        scope.Kind.Should().Be("branch");
        scope.BranchId.Should().Be(branchId);
    }

    [Fact]
    public async Task ResolveAsync_picks_the_highest_ranked_scope_when_multiple_grants_exist()
    {
        await using var db = CreateContext(nameof(ResolveAsync_picks_the_highest_ranked_scope_when_multiple_grants_exist));
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var permissions = new FakePermissionService(
        [
            new EffectivePermission("reports.operational.own", "reports", "operational", "own"),
            new EffectivePermission("reports.operational.all", "reports", "operational", "all"),
        ]);
        var service = new ReportScopeService(db, permissions);

        var scope = await service.ResolveAsync(tenantId, userId, "operational", CancellationToken.None);

        scope.Kind.Should().Be("all");
    }

    private sealed class FakePermissionService(IReadOnlyCollection<EffectivePermission> permissions) : IPermissionService
    {
        public Task<IReadOnlyCollection<EffectivePermission>> GetEffectivePermissionsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult(permissions);

        public Task InvalidateAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<PermissionCatalogItem>> GetCatalogAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PermissionCatalogItem>>([]);

        public Task<IReadOnlyList<EffectivePermissionExplanation>> ExplainEffectivePermissionsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<EffectivePermissionExplanation>>([]);
    }
}
