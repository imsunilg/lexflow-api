using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Portal;

/// <summary>
/// Module 17: "my matters" home list + sanitized per-matter timeline. BR-10: nothing reaches
/// the portal without an explicit publish flag — Hearing.PortalVisible / HearingOutcome.PortalVisible
/// gate every timeline entry; internal notes/strategy content is never queried here at all
/// (there is no code path from this class to Matter.Description or any internal-notes table).
/// </summary>
public sealed class PortalTimelineService(LexFlowDbContext db) : IPortalTimelineService
{
    public async Task<IReadOnlyList<PortalMatterSummaryDto>> GetMyMattersAsync(Guid tenantId, Guid clientId, IReadOnlyCollection<Guid>? visibleMatterIds, CancellationToken cancellationToken = default)
    {
        var matters = await db.Matters
            .Where(m => m.TenantId == tenantId && m.ClientId == clientId)
            .ToListAsync(cancellationToken);

        matters = FilterByVisibility(matters, visibleMatterIds);

        var results = new List<PortalMatterSummaryDto>();
        foreach (var matter in matters)
        {
            var nextHearingDate = await GetNextVisibleHearingDateAsync(tenantId, matter.Id, cancellationToken);
            string? lawyerName = null;
            if (matter.ResponsibleLawyerId is { } lawyerId)
            {
                lawyerName = await db.Users.Where(u => u.Id == lawyerId).Select(u => u.Name).SingleOrDefaultAsync(cancellationToken);
            }

            results.Add(new PortalMatterSummaryDto(matter.Id, matter.Number, matter.Title, matter.Status, nextHearingDate, lawyerName));
        }

        return results;
    }

    public async Task<PortalMatterTimelineDto> GetTimelineAsync(Guid tenantId, Guid clientId, IReadOnlyCollection<Guid>? visibleMatterIds, Guid matterId, CancellationToken cancellationToken = default)
    {
        var matter = await db.Matters.SingleOrDefaultAsync(m => m.TenantId == tenantId && m.Id == matterId, cancellationToken);

        // AC-P1: a matter that exists but belongs to a different client (or is outside this
        // caller's visible-matter subset) is reported as NotFound, never Forbidden — same
        // enumeration-safe convention NotFoundException itself documents for cross-tenant access.
        if (matter is null || matter.ClientId != clientId || (visibleMatterIds is not null && !visibleMatterIds.Contains(matter.Id)))
        {
            throw new NotFoundException(nameof(Matter), matterId);
        }

        var caseIds = await db.CourtCases.Where(c => c.TenantId == tenantId && c.MatterId == matterId).Select(c => c.Id).ToListAsync(cancellationToken);

        var entries = new List<PortalTimelineEntryDto>();

        if (caseIds.Count > 0)
        {
            var hearings = await db.Hearings
                .Where(h => h.TenantId == tenantId && caseIds.Contains(h.CaseId) && h.PortalVisible)
                .ToListAsync(cancellationToken);

            foreach (var hearing in hearings)
            {
                var at = new DateTimeOffset(hearing.Date.ToDateTime(hearing.Time ?? new TimeOnly(7, 0)), TimeSpan.Zero);
                entries.Add(new PortalTimelineEntryDto("Hearing", at, hearing.Purpose ?? "Hearing", hearing.Status));
            }

            var hearingIds = hearings.Select(h => h.Id).ToList();
            var outcomes = await db.HearingOutcomes
                .Where(o => o.TenantId == tenantId && hearingIds.Contains(o.HearingId) && o.PortalVisible)
                .ToListAsync(cancellationToken);

            entries.AddRange(outcomes.Select(o => new PortalTimelineEntryDto("HearingOutcome", o.RecordedAt, "Hearing outcome", o.Summary)));
        }

        var orderedEntries = entries.OrderBy(e => e.At).ToList();
        return new PortalMatterTimelineDto(matter.Id, matter.Number, matter.Title, matter.Status, orderedEntries);
    }

    private async Task<DateOnly?> GetNextVisibleHearingDateAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken)
    {
        var caseIds = await db.CourtCases.Where(c => c.TenantId == tenantId && c.MatterId == matterId).Select(c => c.Id).ToListAsync(cancellationToken);
        if (caseIds.Count == 0)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await db.Hearings
            .Where(h => h.TenantId == tenantId && caseIds.Contains(h.CaseId) && h.PortalVisible && h.Status == "Scheduled" && h.Date >= today)
            .OrderBy(h => h.Date)
            .Select(h => (DateOnly?)h.Date)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static List<Matter> FilterByVisibility(List<Matter> matters, IReadOnlyCollection<Guid>? visibleMatterIds) =>
        visibleMatterIds is null ? matters : matters.Where(m => visibleMatterIds.Contains(m.Id)).ToList();
}
