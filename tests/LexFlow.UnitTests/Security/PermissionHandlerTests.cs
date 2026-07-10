using System.Security.Claims;
using FluentAssertions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;

namespace LexFlow.UnitTests.Security;

/// <summary>
/// G-AC4 (PRD §20(15): IDOR/authorization test suite; §21 header: "the matrix ...
/// drives G-AC4 tests"). Exercises PermissionHandler against every (role, permission)
/// cell implied by PRD §21's Roles &amp; Permissions Matrix, using the exact role -&gt;
/// permission wiring seeded in lexflow-database Scripts/16_Seed/002_System_Roles.sql
/// (see PermissionMatrixFixture). For every role and every permission in the full
/// catalog, asserts the handler succeeds iff that role holds a grant for the same
/// module+action at a scope rank &gt;= the requirement's scope rank (own &lt; team &lt;
/// branch &lt; all — PRD §21 header).
/// </summary>
public sealed class PermissionHandlerTests
{
    public static IEnumerable<object[]> RolePermissionCases()
    {
        foreach (var role in PermissionMatrixFixture.RoleGrants.Keys)
        {
            foreach (var permission in PermissionMatrixFixture.AllPermissionKeys)
            {
                yield return [role, permission, ExpectedAllowed(role, permission)];
            }
        }
    }

    [Theory]
    [MemberData(nameof(RolePermissionCases))]
    public async Task HandleAsync_matches_the_section_21_matrix(string role, string permission, bool expectedAllowed)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var effectivePermissions = PermissionMatrixFixture.RoleGrants[role]
            .Select(ToEffectivePermission)
            .ToArray();

        var fakePermissionService = new FakePermissionService(effectivePermissions);
        var handler = new PermissionHandler(fakePermissionService);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", userId.ToString()),
            new Claim("tenant", tenantId.ToString()),
        ]));

        var requirement = new PermissionRequirement(permission);
        var context = new AuthorizationHandlerContext([requirement], principal, resource: null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().Be(
            expectedAllowed,
            because: $"role '{role}' {(expectedAllowed ? "should" : "should not")} satisfy '{permission}' per PRD §21");
    }

    [Fact]
    public async Task HandleAsync_fails_closed_when_principal_has_no_tenant_or_sub_claim()
    {
        var fakePermissionService = new FakePermissionService([new EffectivePermission("matters.read.all", "matters", "read", "all")]);
        var handler = new PermissionHandler(fakePermissionService);

        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        var requirement = new PermissionRequirement("matters.read.own");
        var context = new AuthorizationHandlerContext([requirement], anonymous, resource: null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    private static readonly Dictionary<string, int> ScopeRank = new()
    {
        ["own"] = 1,
        ["team"] = 2,
        ["branch"] = 3,
        ["all"] = 4,
    };

    /// <summary>
    /// Independent re-derivation of the scope-hierarchy rule (not a call into
    /// PermissionEvaluator) so this test can't pass merely because it shares a bug
    /// with the production code under test.
    /// </summary>
    private static bool ExpectedAllowed(string role, string permissionKey)
    {
        var (module, action, requiredScope) = Split(permissionKey);
        var requiredRank = ScopeRank.GetValueOrDefault(requiredScope, -1);

        foreach (var grantedKey in PermissionMatrixFixture.RoleGrants[role])
        {
            var (grantedModule, grantedAction, grantedScope) = Split(grantedKey);
            if (grantedModule != module || grantedAction != action)
            {
                continue;
            }

            var grantedRank = ScopeRank.GetValueOrDefault(grantedScope, -1);
            if (grantedRank >= requiredRank)
            {
                return true;
            }
        }

        return false;
    }

    private static EffectivePermission ToEffectivePermission(string key)
    {
        var (module, action, scope) = Split(key);
        return new EffectivePermission(key, module, action, scope);
    }

    private static (string Module, string Action, string Scope) Split(string key)
    {
        var segments = key.Split('.');
        return (segments[0], segments[1], segments[2]);
    }

    private sealed class FakePermissionService(IReadOnlyCollection<EffectivePermission> permissions) : IPermissionService
    {
        public Task<IReadOnlyCollection<EffectivePermission>> GetEffectivePermissionsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult(permissions);

        public Task InvalidateAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<PermissionCatalogItem>> GetCatalogAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not exercised by PermissionHandlerTests.");

        public Task<IReadOnlyList<EffectivePermissionExplanation>> ExplainEffectivePermissionsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not exercised by PermissionHandlerTests.");
    }
}
