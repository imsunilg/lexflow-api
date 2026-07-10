using System.Net;
using System.Security.Claims;
using FluentAssertions;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using LexFlow.Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Persistence;

/// <summary>
/// Exercises AuditSaveChangesInterceptor's own diff/redaction/actor-resolution logic
/// against EF Core's InMemory provider — fast, no Docker required. This is a
/// complement to, not a replacement for, LexFlow.IntegrationTests' Testcontainers-based
/// G-AC5 scenario (which proves the same interceptor against the real audit.audit_events
/// table, partitioning, and insert-only triggers); that test could not be executed in
/// this sandbox (no Docker daemon available) but was verified to compile and to fail
/// only on DockerUnavailableException, not on any logic error.
/// </summary>
public sealed class AuditSaveChangesInterceptorTests
{
    private static (LexFlowDbContext Db, Guid TenantId, Guid ActorId) CreateContext(string dbName)
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", actorId.ToString()),
                new Claim("tenant", tenantId.ToString()),
                new Claim("actor_type", "staff"),
            ], "TestAuth")),
        };
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.9");
        httpContext.Request.Headers.UserAgent = "unit-test-agent/1.0";

        var accessor = new StubHttpContextAccessor { HttpContext = httpContext };
        var interceptor = new AuditSaveChangesInterceptor(accessor);

        var options = new DbContextOptionsBuilder<LexFlowDbContext>()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(interceptor)
            .Options;

        return (new LexFlowDbContext(options), tenantId, actorId);
    }

    [Fact]
    public async Task Adding_an_entity_writes_a_single_create_audit_row_with_redacted_password()
    {
        var (db, tenantId, actorId) = CreateContext(nameof(Adding_an_entity_writes_a_single_create_audit_row_with_redacted_password));
        await using var _ = db;

        var user = new User(tenantId, "aditi@example.com", "Aditi Rao");
        user.SetPasswordHash("super-secret-hash-value");

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var auditRows = await db.AuditEvents.Where(e => e.TenantId == tenantId).ToListAsync();

        auditRows.Should().ContainSingle();
        var row = auditRows[0];

        row.Action.Should().Be("create");
        row.EntityType.Should().Be("core.users");
        row.EntityId.Should().Be(user.Id);
        row.Before.Should().BeNull();
        row.After.Should().Contain("***REDACTED***");
        row.After.Should().NotContain("super-secret-hash-value");
        row.ActorUserId.Should().Be(actorId);
        row.ActorType.Should().Be("staff");
        row.Ip.Should().Be("198.51.100.9");
        row.Ua.Should().Be("unit-test-agent/1.0");
    }

    [Fact]
    public async Task Updating_an_entity_writes_a_single_update_audit_row_with_both_before_and_after()
    {
        var (db, tenantId, _) = CreateContext(nameof(Updating_an_entity_writes_a_single_update_audit_row_with_both_before_and_after));
        await using var _disposeDb = db;

        var role = new Role(tenantId, "lawyer", "Lawyer");
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        db.Entry(role).Property("Name").CurrentValue = "Senior Lawyer";
        await db.SaveChangesAsync();

        var updateRow = await db.AuditEvents
            .Where(e => e.TenantId == tenantId && e.Action == "update")
            .SingleAsync();

        updateRow.EntityType.Should().Be("core.roles");
        updateRow.EntityId.Should().Be(role.Id);
        updateRow.Before.Should().Contain("Lawyer").And.NotContain("Senior Lawyer");
        updateRow.After.Should().Contain("Senior Lawyer");
    }

    [Fact]
    public async Task Removing_an_entity_writes_a_single_delete_audit_row_with_only_before()
    {
        var (db, tenantId, _) = CreateContext(nameof(Removing_an_entity_writes_a_single_delete_audit_row_with_only_before));
        await using var _disposeDb = db;

        var branch = new Branch(tenantId, "Delhi Branch", "DEL");
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        db.Branches.Remove(branch);
        await db.SaveChangesAsync();

        var deleteRow = await db.AuditEvents
            .Where(e => e.TenantId == tenantId && e.Action == "delete")
            .SingleAsync();

        deleteRow.EntityType.Should().Be("core.branches");
        deleteRow.EntityId.Should().Be(branch.Id);
        deleteRow.Before.Should().NotBeNull();
        deleteRow.After.Should().BeNull();
    }

    [Fact]
    public async Task Composite_key_join_entities_get_a_null_entity_id()
    {
        var (db, tenantId, _) = CreateContext(nameof(Composite_key_join_entities_get_a_null_entity_id));
        await using var _disposeDb = db;

        var user = new User(tenantId, "paralegal@example.com", "Priya");
        var role = new Role(tenantId, "paralegal", "Paralegal");
        db.AddRange(user, role);
        await db.SaveChangesAsync();

        db.UserRoles.Add(new UserRole(tenantId, user.Id, role.Id));
        await db.SaveChangesAsync();

        var row = await db.AuditEvents
            .Where(e => e.TenantId == tenantId && e.EntityType == "core.user_roles")
            .SingleAsync();

        row.EntityId.Should().BeNull();
    }

    [Fact]
    public async Task No_HttpContext_attributes_the_mutation_to_system()
    {
        var interceptor = new AuditSaveChangesInterceptor(new StubHttpContextAccessor { HttpContext = null });
        var options = new DbContextOptionsBuilder<LexFlowDbContext>()
            .UseInMemoryDatabase(nameof(No_HttpContext_attributes_the_mutation_to_system))
            .AddInterceptors(interceptor)
            .Options;

        await using var db = new LexFlowDbContext(options);
        var tenantId = Guid.NewGuid();
        var branch = new Branch(tenantId, "Pune Branch", "PUN");

        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var row = await db.AuditEvents.Where(e => e.TenantId == tenantId).SingleAsync();
        row.ActorType.Should().Be("system");
        row.ActorUserId.Should().BeNull();
    }

    private sealed class StubHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
}
