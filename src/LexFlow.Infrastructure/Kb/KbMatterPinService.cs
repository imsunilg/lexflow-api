using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Kb;

/// <summary>
/// Module 12: pin-to-matter. AC-KB4/edge case: "orphan pins after KB item unpublish (pin retains
/// snapshot text)" — <see cref="PinAsync"/> resolves the source item's current text and freezes it
/// into snapshot_text at pin time; every later read of a pin uses that snapshot, never the live
/// KB item, so unpublishing/editing/deleting the source never affects an existing pin.
/// </summary>
public sealed class KbMatterPinService(LexFlowDbContext db) : IKbMatterPinService
{
    public async Task<KbMatterPinDto> PinAsync(Guid tenantId, Guid matterId, Guid? pinnedBy, string kbRefKind, Guid kbRefId, string? note, CancellationToken cancellationToken = default)
    {
        var snapshotText = await ResolveSnapshotTextAsync(tenantId, kbRefKind, kbRefId, cancellationToken)
            ?? throw new NotFoundException(kbRefKind, kbRefId);

        var pin = new KbMatterPin(tenantId, matterId, kbRefKind, kbRefId, note, snapshotText, pinnedBy);
        await db.KbMatterPins.AddAsync(pin, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(pin);
    }

    public async Task UnpinAsync(Guid tenantId, Guid pinId, CancellationToken cancellationToken = default)
    {
        var pin = await db.KbMatterPins.SingleOrDefaultAsync(p => p.TenantId == tenantId && p.Id == pinId, cancellationToken);
        if (pin is null)
        {
            return;
        }

        db.KbMatterPins.Remove(pin);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<KbMatterPinDto>> GetForMatterAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
    {
        var pins = await db.KbMatterPins.Where(p => p.TenantId == tenantId && p.MatterId == matterId).OrderByDescending(p => p.PinnedAt).ToListAsync(cancellationToken);
        return pins.Select(ToDto).ToList();
    }

    public async Task<int> GetPinCountAsync(Guid tenantId, string kbRefKind, Guid kbRefId, CancellationToken cancellationToken = default)
        => await db.KbMatterPins.Where(p => p.TenantId == tenantId && p.KbRefKind == kbRefKind && p.KbRefId == kbRefId).Select(p => p.MatterId).Distinct().CountAsync(cancellationToken);

    private async Task<string?> ResolveSnapshotTextAsync(Guid tenantId, string kbRefKind, Guid kbRefId, CancellationToken cancellationToken)
    {
        switch (kbRefKind)
        {
            case "Act":
                var act = await db.KbActs.SingleOrDefaultAsync(a => a.TenantId == tenantId && a.Id == kbRefId, cancellationToken);
                return act is null ? null : act.Name;

            case "ActSection":
                var section = await db.KbActSections.SingleOrDefaultAsync(s => s.TenantId == tenantId && s.Id == kbRefId, cancellationToken);
                return section is null ? null : $"{section.Number}. {section.Title}\n\n{section.Body}".Trim();

            case "Judgment":
                var judgment = await db.KbJudgments.SingleOrDefaultAsync(j => j.TenantId == tenantId && j.Id == kbRefId, cancellationToken);
                return judgment is null ? null : (judgment.Headnote ?? judgment.Citation);

            case "Article":
                var article = await db.KbArticles.SingleOrDefaultAsync(a => a.TenantId == tenantId && a.Id == kbRefId, cancellationToken);
                return article is null ? null : $"{article.Title}\n\n{article.Body}".Trim();

            default:
                return null;
        }
    }

    private static KbMatterPinDto ToDto(KbMatterPin p) => new(p.Id, p.MatterId, p.KbRefKind, p.KbRefId, p.Note, p.SnapshotText, p.PinnedBy, p.PinnedAt);
}
