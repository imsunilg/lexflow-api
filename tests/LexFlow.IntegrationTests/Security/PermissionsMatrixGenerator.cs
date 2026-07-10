using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using LexFlow.Infrastructure.Security;
using LexFlow.UnitTests.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace LexFlow.IntegrationTests.Security;

/// <summary>
/// G-AC4: "Permission matrix test generates (role × endpoint) grid from §21 and asserts
/// allow/deny automatically; any drift fails CI." §21's own footnote mandates the artifact
/// name: "the matrix is machine-readable (permissions_matrix.json in repo) and drives G-AC4
/// tests." <see cref="PermissionMatrixFixture"/> (tests/LexFlow.UnitTests/Security) is the
/// single hand-maintained transcription of §21 + the lexflow-database seed scripts — this
/// generator reflects over every live [RequirePermission]-attributed controller action in
/// LexFlow.Api and, for each of the 10 staff roles (Client(Portal) is excluded — it has no
/// core.roles row and uses a wholly different ownership-based authorization model, already
/// covered by tests/LexFlow.UnitTests/Portal/PortalIdorTests.cs), records whether that role's
/// §21-derived grant set satisfies the endpoint's requirement via the exact same
/// <see cref="PermissionEvaluator.Grants"/> the real authorization pipeline uses.
///
/// Public/webhook/pre-auth controllers are excluded: they carry no [RequirePermission] at all
/// (public lead capture, public document share links, signature/generic webhooks, auth/session
/// self-service) so the reflection pass naturally skips them without an explicit denylist.
/// </summary>
public static class PermissionsMatrixGenerator
{
    public static PermissionsMatrixDocument Generate(Assembly apiAssembly)
    {
        var roles = PermissionMatrixFixture.RoleGrants.Keys.OrderBy(r => r, StringComparer.Ordinal).ToList();

        var endpoints = apiAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(controllerType => controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(method => BuildEndpoint(controllerType, method, roles))
                .Where(e => e is not null))
            .Select(e => e!)
            .OrderBy(e => e.Controller, StringComparer.Ordinal)
            .ThenBy(e => e.Action, StringComparer.Ordinal)
            .ToList();

        return new PermissionsMatrixDocument(roles, endpoints);
    }

    private static PermissionsMatrixEndpoint? BuildEndpoint(Type controllerType, MethodInfo method, IReadOnlyList<string> roles)
    {
        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>() ?? controllerType.GetCustomAttribute<RequirePermissionAttribute>();
        if (permissionAttribute is null)
        {
            return null;
        }

        var httpMethods = method.GetCustomAttributes()
            .OfType<IActionHttpMethodProvider>()
            .SelectMany(a => a.HttpMethods)
            .Distinct()
            .OrderBy(m => m, StringComparer.Ordinal)
            .ToList();
        if (httpMethods.Count == 0)
        {
            return null;
        }

        var controllerRoute = controllerType.GetCustomAttribute<RouteAttribute>()?.Template ?? string.Empty;
        var actionRoute = method.GetCustomAttributes().OfType<IRouteTemplateProvider>().FirstOrDefault()?.Template ?? string.Empty;
        var route = string.Join("/", new[] { controllerRoute, actionRoute }.Where(s => !string.IsNullOrEmpty(s)));

        var permission = permissionAttribute.Policy![RequirePermissionAttribute.PolicyPrefix.Length..];
        var segments = permission.Split('.');
        if (segments.Length != 3)
        {
            // A permission string that can't even be parsed into module.action.scope would
            // throw inside PermissionRequirement's own constructor the first time a real
            // request hit this endpoint (see that type) — surfacing it here as IsWellFormed
            // = false is far cheaper than discovering it via a 500 in production.
            return new PermissionsMatrixEndpoint(controllerType.Name, method.Name, httpMethods, route, permission, IsWellFormed: false, []);
        }

        var (module, action, scope) = (segments[0], segments[1], segments[2]);
        var allowedRoles = roles.Where(role => PermissionEvaluator.Grants(ToEffectivePermissions(PermissionMatrixFixture.RoleGrants[role]), module, action, scope)).ToList();

        return new PermissionsMatrixEndpoint(controllerType.Name, method.Name, httpMethods, route, permission, IsWellFormed: true, allowedRoles);
    }

    private static IReadOnlyCollection<Application.Common.Interfaces.EffectivePermission> ToEffectivePermissions(IReadOnlyList<string> keys) =>
        keys.Select(key =>
        {
            var parts = key.Split('.');
            return new Application.Common.Interfaces.EffectivePermission(key, parts[0], parts[1], parts[2]);
        }).ToList();

    public static string ToJson(PermissionsMatrixDocument document) => JsonSerializer.Serialize(document, new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    });
}

public sealed record PermissionsMatrixDocument(IReadOnlyList<string> Roles, IReadOnlyList<PermissionsMatrixEndpoint> Endpoints);

public sealed record PermissionsMatrixEndpoint(string Controller, string Action, IReadOnlyList<string> HttpMethods, string Route, string RequiredPermission, bool IsWellFormed, IReadOnlyList<string> AllowedRoles);
