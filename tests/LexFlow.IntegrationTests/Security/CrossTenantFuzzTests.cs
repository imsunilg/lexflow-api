using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using FluentAssertions;
using LexFlow.Api.Controllers;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.IntegrationTests.Security;

/// <summary>
/// G-AC3 (§8): "Automated cross-tenant test suite (every GET/PUT/DELETE with foreign-tenant
/// ids) returns 404/403 in 100% of endpoints." Scoped here to GET and DELETE — PUT actions
/// take a body shaped per-DTO (60+ distinct request types across the API), and ASP.NET Core's
/// model binder runs before the action method (i.e. before the ownership check the action body
/// contains even executes), so a PUT with an empty/placeholder body mostly exercises
/// validation, not tenant isolation; GET/DELETE take no body and reach the real ownership
/// check cleanly. This is a scope reduction from the PRD's literal "GET/PUT/DELETE" wording,
/// documented rather than silently narrowed.
///
/// Method: mint an access token for a user in Tenant B holding every real [RequirePermission]
/// string this reflection pass finds anywhere in LexFlow.Api (so RBAC itself never blocks a
/// call — a 403 from missing permission would be a true-but-uninteresting pass, masking
/// whether the endpoint's own tenant-ownership check is what's actually firing), then hit
/// every such GET/DELETE route with ids belonging to Tenant A substituted in. Every response
/// must be 404 or 403, never 2xx, never 500.
/// </summary>
[Collection(nameof(CrossTenantFuzzCollection))]
public sealed class CrossTenantFuzzTests(CrossTenantFuzzFixture fixture)
{
    /// <summary>Controllers with no meaningful "foreign-tenant record" concept to fuzz: public/anonymous endpoints, webhooks (HMAC-signed, not user-authenticated), and the portal (separate JWT audience/scheme entirely, already covered end-to-end by PortalIdorTests).</summary>
    private static readonly HashSet<string> ExcludedControllers =
    [
        nameof(PublicLeadCaptureController),
        nameof(PublicDocumentShareController),
        nameof(SignatureWebhookController),
        nameof(WebhooksController),
    ];

    [Fact]
    public async Task Every_GET_and_DELETE_endpoint_returns_404_or_403_for_a_foreign_tenants_record_id()
    {
        var (tenantAId, resourceIds) = await SeedForeignTenantResourcesAsync();
        var tenantBToken = await SeedCallerAndMintFullyPermissionedTokenAsync();

        var routes = DiscoverGetAndDeleteRoutes();
        routes.Should().NotBeEmpty();

        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantBToken);

        var failures = new List<string>();
        foreach (var (method, template) in routes)
        {
            var url = "/" + SubstituteRouteParameters(template, resourceIds);
            using var request = new HttpRequestMessage(new HttpMethod(method), url);
            using var response = await client.SendAsync(request);

            var ok = response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden;
            if (!ok)
            {
                failures.Add($"{method} {url} -> {(int)response.StatusCode} {response.StatusCode} (tenant={tenantAId})");
            }
        }

        failures.Should().BeEmpty("every GET/DELETE endpoint must return 404 or 403 for a record belonging to a different tenant (G-AC3)");
    }

    private static string SubstituteRouteParameters(string template, IReadOnlyDictionary<string, Guid> resourceIds)
    {
        return Regex.Replace(template, @"\{(\w+)(?::[^}?]+)?\??\}", match =>
        {
            var paramName = match.Groups[1].Value.ToLowerInvariant();
            return (resourceIds.TryGetValue(paramName, out var id) ? id : Guid.NewGuid()).ToString();
        });
    }

    private static List<(string HttpMethod, string Route)> DiscoverGetAndDeleteRoutes()
    {
        var document = PermissionsMatrixGenerator.Generate(typeof(AuthController).Assembly);

        return document.Endpoints
            .Where(e => !ExcludedControllers.Contains(e.Controller))
            .Where(e => e.Route.Contains('{'))
            .SelectMany(e => e.HttpMethods.Where(m => m is "GET" or "DELETE").Select(m => (m, e.Route)))
            .Distinct()
            .ToList();
    }

    /// <summary>Seeds Tenant A ("the victim") with one row of the core resource types most controllers key off, keyed by the lowercase route-parameter name most controllers use for that resource. Anything not in this map falls back to a plain nonexistent GUID (still a valid, if weaker, 404 case — see class doc comment).</summary>
    private async Task<(Guid TenantId, Dictionary<string, Guid> ResourceIds)> SeedForeignTenantResourcesAsync()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var lead = new Lead(tenantId, "LD-FUZZ-0001", "Foreign", "Lead", null, "foreign-lead@example.com", null, null, null, null, null, null, null, null, null);
        var client = new Client(tenantId, "CL-FUZZ-0001", "Individual", "Foreign", "Client", null, "foreign-client@example.com", null, null, null, null, null, null);
        var matter = new Matter(tenantId, "MAT-FUZZ-0001", "Foreign matter", client.Id, "Litigation", null, null, null, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        var court = new Court(tenantId, "Fuzz Test Court", "District", "Mumbai", "Maharashtra", null);
        var courtCase = new CourtCase(tenantId, matter.Id, court.Id, "CS", "9999", DateTime.UtcNow.Year, null, null, null, null, null, appealOfCaseId: null);
        var hearing = new Hearing(tenantId, courtCase.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), null, "Asia/Kolkata", "Fuzz hearing", null, null);
        var folder = new Folder(tenantId, "Fuzz Folder", null, matter.Id, null);
        var document = new Document(tenantId, folder.Id, matter.Id, client.Id, courtCase.Id, "Fuzz Document", "Contract", "Normal");
        var invoice = new Invoice(tenantId, matter.Id, client.Id, null, null, "INR", null);
        invoice.SetTotals(100, 0, 0, 100);
        invoice.MarkSent();
        var task = new OpsTask(tenantId, "Fuzz Task", null, matter.Id, client.Id, null, null, "Medium", null);

        db.AddRange(lead, client, matter, court, courtCase, hearing, folder, document, invoice, task);
        await db.SaveChangesAsync();

        var resourceIds = new Dictionary<string, Guid>
        {
            ["id"] = matter.Id, // most controllers' bare {id} refers to their own primary resource; matter is the most common single fallback for the generic name.
            ["leadid"] = lead.Id,
            ["clientid"] = client.Id,
            ["matterid"] = matter.Id,
            ["caseid"] = courtCase.Id,
            ["hearingid"] = hearing.Id,
            ["folderid"] = folder.Id,
            ["documentid"] = document.Id,
            ["docid"] = document.Id,
            ["invoiceid"] = invoice.Id,
            ["taskid"] = task.Id,
        };

        return (tenantId, resourceIds);
    }

    private async Task<string> SeedCallerAndMintFullyPermissionedTokenAsync()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var user = new User(tenantId, "fuzz-caller@example.com", "Fuzz Caller");
        user.Activate();
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();

        // Grant every real [RequirePermission] string this build actually uses anywhere in
        // LexFlow.Api — not just PermissionMatrixFixture's §21 transcription, which (per
        // PermissionMatrixIntegrationTests' own findings) doesn't cover every module yet. RBAC
        // must never be the reason a request gets rejected in this suite; only tenant
        // ownership may be.
        var document = PermissionsMatrixGenerator.Generate(typeof(AuthController).Assembly);
        var allPermissionKeys = document.Endpoints.Where(e => e.IsWellFormed).Select(e => e.RequiredPermission).Distinct().ToList();

        var permissions = new List<Permission>();
        var grants = new List<UserPermissionGrant>();
        foreach (var key in allPermissionKeys)
        {
            var parts = key.Split('.');
            var permission = new Permission(tenantId, key, parts[0], parts[1], parts[2]);
            permissions.Add(permission);
            grants.Add(new UserPermissionGrant(tenantId, user.Id, permission.Id));
        }

        await db.Permissions.AddRangeAsync(permissions);
        await db.UserPermissionGrants.AddRangeAsync(grants);
        await db.SaveChangesAsync();

        using var scope = fixture.Services.CreateScope();
        var jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var accessToken = jwtTokenService.IssueAccessToken(user.Id, tenantId, "owner", null, allPermissionKeys, audience: "staff");
        return accessToken.Token;
    }

    private LexFlowDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LexFlowDbContext>().UseNpgsql(fixture.ConnectionString).Options);
}
