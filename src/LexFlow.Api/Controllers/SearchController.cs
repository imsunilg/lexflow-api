using LexFlow.Api.Contracts;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Clients;
using LexFlow.Application.Queries.Leads;
using LexFlow.Application.Queries.Matters;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>
/// GET /api/v1/search?q= — the staff shell's global search overlay (PRD §26). Did not
/// exist server-side at all before this (`SearchService`/`search-overlay.component.ts`
/// on the frontend were already fully built against this exact contract and 404'd on
/// every call). Deliberately does not introduce a new search index/query path: it fans
/// out to the same `GetLeadsQuery`/`GetMattersQuery`/`GetClientsQuery` handlers the
/// Leads/Matters/Clients list pages already call with their own `q` filter, and reuses
/// each list's real `/:id` detail route to build result URLs — so a picked result always
/// opens the actual record, not a guess. Scoped to these three because they're the only
/// entities with both a working server-side `q` filter and a URL-addressable detail
/// route on the frontend; Tasks has no `q` filter and Documents has no by-id route, so
/// including either would list a match a click couldn't actually open correctly.
///
/// [Authorize] only, not [RequirePermission]: each entity type is fanned out to only if
/// the caller's own effective permissions grant its list endpoint's own requirement
/// (e.g. `leads.read.all`), checked here the same way `PermissionHandler` checks it for
/// those controllers directly — a single fixed permission on this action would either
/// block everyone from some group or leak groups no permission actually grants.
/// </summary>
[ApiController]
[Route("api/v1/search")]
[Authorize]
public sealed class SearchController(
    IMediator mediator,
    IPermissionService permissionService,
    ICurrentUserService currentUser) : ControllerBase
{
    private const int MaxPerGroup = 5;

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var query = q?.Trim();
        if (string.IsNullOrEmpty(query))
        {
            return Ok(ApiResponse<IReadOnlyList<SearchResultGroupDto>>.Of([]));
        }

        var permissions = await permissionService.GetEffectivePermissionsAsync(
            currentUser.UserId!.Value, currentUser.TenantId!.Value, cancellationToken);

        var groups = new List<SearchResultGroupDto>();

        if (PermissionEvaluator.Grants(permissions, "leads", "read", "all"))
        {
            var leads = await mediator.Send(new GetLeadsQuery(null, null, null, null, null, null, query, null), cancellationToken);
            AddGroup(groups, "leads", "Leads", leads.Take(MaxPerGroup).Select(lead => new SearchResultItemDto(
                "leads",
                lead.Id,
                lead.LastName is null ? lead.FirstName : $"{lead.FirstName} {lead.LastName}",
                lead.Company,
                $"/leads/{lead.Id}")));
        }

        if (PermissionEvaluator.Grants(permissions, "matters", "read", "all"))
        {
            var matters = await mediator.Send(new GetMattersQuery(null, null, null, null, null, query), cancellationToken);
            AddGroup(groups, "matters", "Matters", matters.Take(MaxPerGroup).Select(matter => new SearchResultItemDto(
                "matters",
                matter.Id,
                $"{matter.Number} — {matter.Title}",
                matter.Status,
                $"/matters/{matter.Id}")));
        }

        if (PermissionEvaluator.Grants(permissions, "clients", "read", "all"))
        {
            var clients = await mediator.Send(new GetClientsQuery(null, null, null, null, query), cancellationToken);
            AddGroup(groups, "clients", "Clients", clients.Take(MaxPerGroup).Select(client => new SearchResultItemDto(
                "clients",
                client.Id,
                client.DisplayName ?? client.LegalName ?? $"{client.FirstName} {client.LastName}".Trim(),
                client.Email,
                $"/clients/{client.Id}")));
        }

        return Ok(ApiResponse<IReadOnlyList<SearchResultGroupDto>>.Of(groups));
    }

    private static void AddGroup(List<SearchResultGroupDto> groups, string type, string label, IEnumerable<SearchResultItemDto> items)
    {
        var results = items.ToList();
        if (results.Count > 0)
        {
            groups.Add(new SearchResultGroupDto(type, label, results));
        }
    }
}

public sealed record SearchResultItemDto(string Type, Guid Id, string Title, string? Subtitle, string Url);

public sealed record SearchResultGroupDto(string Type, string Label, IReadOnlyList<SearchResultItemDto> Items);
