using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using FluentAssertions;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using LexFlow.Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.IntegrationTests.Audit;

/// <summary>
/// G-AC5 (PRD §30 Capture: "EF Core SaveChanges interceptor (single choke point)
/// ... same-transaction write — no async loss"). Runs a scripted create → update →
/// delete scenario across every entity type LexFlow.Domain currently models that can
/// be constructed from application code (12 — core.tenants itself is provisioned by
/// a not-yet-built onboarding flow and has no public constructor, so it's excluded;
/// tenant_id carries no physical FK per the schema's own design, so this doesn't
/// require a real tenant row to exist) and asserts audit.audit_events has exactly
/// one row per (entity type, action) combination — i.e. zero gaps. Because the
/// interceptor is driven entirely by EF metadata (schema/table name, primary key
/// shape, TenantId property) rather than a hardcoded entity list, this scenario will
/// keep proving the same "no gaps" property as later Build Playbook prompts add the
/// remaining entity types toward the PRD's full 25.
/// </summary>
[Collection(nameof(AuditTrailCollection))]
public sealed class AuditTrailIntegrationTests(AuditTrailFixture fixture)
{
    private static readonly (string Schema, string Table)[] ExpectedEntityTypes =
    [
        ("core", "branches"),
        ("core", "departments"),
        ("core", "teams"),
        ("core", "users"),
        ("core", "roles"),
        ("core", "permissions"),
        ("core", "team_members"),
        ("core", "user_roles"),
        ("core", "role_permissions"),
        ("core", "user_permission_grants"),
        ("core", "user_sessions"),
        ("core", "login_history"),
    ];

    private static readonly string[] ExpectedActions = ["create", "update", "delete"];

    [Fact]
    public async Task Scripted_scenario_across_every_modeled_entity_type_produces_zero_audit_gaps()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        using var activity = new Activity("AuditTrailIntegrationTest").Start();

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", actorId.ToString()),
                new Claim("tenant", tenantId.ToString()),
                new Claim("actor_type", "staff"),
            ], "TestAuth")),
        };
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        httpContext.Request.Headers.UserAgent = "xunit-audit-scenario/1.0";

        var accessor = new FakeHttpContextAccessor { HttpContext = httpContext };
        var interceptor = new AuditSaveChangesInterceptor(accessor);

        var options = new DbContextOptionsBuilder<LexFlowDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;

        await using var db = new LexFlowDbContext(options);

        // --- Phase 1: create (two batches so join-table FKs resolve against
        // already-committed parent rows within the same overall scenario). ---
        var branch = new Branch(tenantId, "Mumbai HQ", "MUM");
        var department = new Department(tenantId, "Litigation");
        var team = new Team(tenantId, "Banking Litigation Team");
        var user = new User(tenantId, $"{Guid.NewGuid():N}@example.com", "Aditi Rao");
        user.SetPasswordHash("password_hash_value_should_never_appear");
        var role = new Role(tenantId, "lawyer", "Lawyer");
        var permission = new Permission(tenantId, "matters.read.own", "matters", "read", "own");

        db.AddRange(branch, department, team, user, role, permission);
        await db.SaveChangesAsync();

        var userRole = new UserRole(tenantId, user.Id, role.Id);
        var rolePermission = new RolePermission(tenantId, role.Id, permission.Id);
        var userPermissionGrant = new UserPermissionGrant(tenantId, user.Id, permission.Id);
        var teamMember = new TeamMember(tenantId, team.Id, user.Id);
        var userSession = new UserSession(tenantId, user.Id, "refresh-hash-value", Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(7));
        var loginHistory = new LoginHistory(tenantId, user.Id, "Success", "203.0.113.7", "xunit-audit-scenario/1.0");

        db.AddRange(userRole, rolePermission, userPermissionGrant, teamMember, userSession, loginHistory);
        await db.SaveChangesAsync();

        // --- Phase 2: update (touch UpdatedAt/UpdatedBy on every entity in one shot). ---
        object[] allEntities =
        [
            branch, department, team, user, role, permission,
            userRole, rolePermission, userPermissionGrant, teamMember, userSession, loginHistory,
        ];

        foreach (var entity in allEntities)
        {
            var entry = db.Entry(entity);
            entry.Property("UpdatedAt").CurrentValue = DateTimeOffset.UtcNow;
            entry.Property("UpdatedBy").CurrentValue = actorId;
        }

        await db.SaveChangesAsync();

        // --- Phase 3: delete (children first, then parents). ---
        db.RemoveRange(userSession, loginHistory, teamMember, userRole, rolePermission, userPermissionGrant);
        await db.SaveChangesAsync();

        db.RemoveRange(user, role, permission, team, department, branch);
        await db.SaveChangesAsync();

        // --- Assert: exactly one audit row per (entity type, action) — zero gaps. ---
        var auditRows = await db.AuditEvents
            .Where(e => e.TenantId == tenantId)
            .ToListAsync();

        auditRows.Should().HaveCount(ExpectedEntityTypes.Length * ExpectedActions.Length);

        foreach (var (schema, table) in ExpectedEntityTypes)
        {
            var entityType = $"{schema}.{table}";
            foreach (var action in ExpectedActions)
            {
                auditRows.Count(e => e.EntityType == entityType && e.Action == action)
                    .Should().Be(1, because: $"'{entityType}' should have exactly one '{action}' audit row");
            }
        }

        // --- Assert: actor/ip/ua/trace captured on every row. ---
        auditRows.Should().OnlyContain(e =>
            e.ActorUserId == actorId
            && e.ActorType == "staff"
            && e.Ip == "203.0.113.7"
            && e.Ua == "xunit-audit-scenario/1.0"
            && e.TraceId == activity.Id);

        // --- Assert: single-Guid-PK entities got an entity_id; composite-PK join tables didn't. ---
        string[] singleKeyEntityTypes = ["core.branches", "core.departments", "core.teams", "core.users", "core.roles", "core.permissions", "core.user_sessions", "core.login_history"];
        string[] compositeKeyEntityTypes = ["core.team_members", "core.user_roles", "core.role_permissions", "core.user_permission_grants"];

        auditRows.Where(e => singleKeyEntityTypes.Contains(e.EntityType)).Should().OnlyContain(e => e.EntityId != null);
        auditRows.Where(e => compositeKeyEntityTypes.Contains(e.EntityType)).Should().OnlyContain(e => e.EntityId == null);

        // --- Assert: redaction — the user's password hash never appears in plaintext. ---
        var userCreateRow = auditRows.Single(e => e.EntityType == "core.users" && e.Action == "create");
        userCreateRow.After.Should().Contain("***REDACTED***");
        userCreateRow.After.Should().NotContain("password_hash_value_should_never_appear");
    }
}
