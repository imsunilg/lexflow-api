using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Kb;

/// <summary>
/// Module 12: Acts/Sections CRUD with as-on-date historical rendering (AC-KB3). Validation Rules:
/// "section numbers unique within act" is enforced first at the DB (a partial unique index on
/// (act_id, number) WHERE is_deleted = false) and again defensively here — a DbUpdateException
/// carrying that index's name is translated to DomainRuleException("SECTION_NUMBER_NOT_UNIQUE",
/// ...), defense in depth matching this module's own build brief.
/// </summary>
public sealed class KbActService(LexFlowDbContext db) : IKbActService
{
    public async Task<KbActDto> CreateActAsync(Guid tenantId, string name, string? shortCode, string? jurisdiction, int? year, CancellationToken cancellationToken = default)
    {
        var act = new KbAct(tenantId, name, shortCode, jurisdiction, year);
        await db.KbActs.AddAsync(act, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(act);
    }

    public async Task<KbActDto> UpdateActAsync(Guid tenantId, Guid actId, string name, string? shortCode, string? jurisdiction, int? year, CancellationToken cancellationToken = default)
    {
        var act = await GetActOrThrowAsync(tenantId, actId, cancellationToken);
        act.Update(name, shortCode, jurisdiction, year);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(act);
    }

    public async Task<IReadOnlyList<KbActDto>> GetActsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var acts = await db.KbActs.Where(a => a.TenantId == tenantId).OrderBy(a => a.Name).ToListAsync(cancellationToken);
        return acts.Select(ToDto).ToList();
    }

    public async Task<KbActDto?> GetActAsync(Guid tenantId, Guid actId, CancellationToken cancellationToken = default)
    {
        var act = await db.KbActs.SingleOrDefaultAsync(a => a.TenantId == tenantId && a.Id == actId, cancellationToken);
        return act is null ? null : ToDto(act);
    }

    public async Task<KbActSectionDto> CreateSectionAsync(Guid tenantId, Guid actId, Guid? parentId, string number, string? title, string? body, DateOnly? effectiveFrom, CancellationToken cancellationToken = default)
    {
        await GetActOrThrowAsync(tenantId, actId, cancellationToken);

        KbActSection? parent = null;
        if (parentId.HasValue)
        {
            parent = await db.KbActSections.SingleOrDefaultAsync(s => s.TenantId == tenantId && s.Id == parentId, cancellationToken)
                ?? throw new NotFoundException(nameof(KbActSection), parentId.Value);
        }

        // Validation Rules: "section numbers unique within act" — checked here first (the DB's
        // own partial unique index is the authoritative backstop against a concurrent race; this
        // check is what makes the rule testable under EF InMemory, which enforces no such index).
        var numberTaken = await db.KbActSections.AnyAsync(s => s.TenantId == tenantId && s.ActId == actId && s.Number == number, cancellationToken);
        if (numberTaken)
        {
            throw new DomainRuleException("SECTION_NUMBER_NOT_UNIQUE", $"Section number '{number}' already exists in this act (Module 12 Validation Rules).");
        }

        var section = new KbActSection(tenantId, actId, parentId, number, title, body, effectiveFrom, null, null);
        section.SetPath(BuildPath(parent, section.Id));

        try
        {
            await db.KbActSections.AddAsync(section, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsSectionNumberConflict(ex))
        {
            throw new DomainRuleException("SECTION_NUMBER_NOT_UNIQUE", $"Section number '{number}' already exists in this act (Module 12 Validation Rules).");
        }

        return ToDto(section);
    }

    public async Task<IReadOnlyList<KbActSectionDto>> GetSectionsAsync(Guid tenantId, Guid actId, CancellationToken cancellationToken = default)
    {
        var sections = await db.KbActSections.Where(s => s.TenantId == tenantId && s.ActId == actId).OrderBy(s => s.Number).ToListAsync(cancellationToken);
        return sections.Select(ToDto).ToList();
    }

    public async Task<KbActSectionDto?> GetSectionAsync(Guid tenantId, Guid sectionId, CancellationToken cancellationToken = default)
    {
        var section = await db.KbActSections.SingleOrDefaultAsync(s => s.TenantId == tenantId && s.Id == sectionId, cancellationToken);
        return section is null ? null : ToDto(section);
    }

    public async Task<KbActSectionDto?> LookupSectionAsync(Guid tenantId, string actShortCodeOrName, string number, CancellationToken cancellationToken = default)
    {
        var act = await db.KbActs.SingleOrDefaultAsync(
            a => a.TenantId == tenantId && (a.ShortCode == actShortCodeOrName || a.Name == actShortCodeOrName), cancellationToken);
        if (act is null)
        {
            return null;
        }

        var section = await db.KbActSections.SingleOrDefaultAsync(s => s.TenantId == tenantId && s.ActId == act.Id && s.Number == number, cancellationToken);
        return section is null ? null : ToDto(section);
    }

    public async Task<KbActSectionDto?> GetSectionAsOfAsync(Guid tenantId, Guid actId, string number, DateOnly asOfDate, CancellationToken cancellationToken = default)
    {
        // Historical rows are soft-deleted (see KbActSection.CloseForAmendment) — the default
        // query filter would hide them, so this is the one read path that must bypass it.
        var candidates = await db.KbActSections.IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId && s.ActId == actId && s.Number == number)
            .ToListAsync(cancellationToken);

        var match = candidates.SingleOrDefault(s =>
            (s.EffectiveFrom is null || s.EffectiveFrom <= asOfDate) &&
            (s.EffectiveTo is null || s.EffectiveTo > asOfDate));

        return match is null ? null : ToDto(match);
    }

    public async Task<KbActSectionDto> AmendSectionAsync(Guid tenantId, Guid sectionId, string? newTitle, string? newBody, DateOnly amendedOn, CancellationToken cancellationToken = default)
    {
        var current = await db.KbActSections.SingleOrDefaultAsync(s => s.TenantId == tenantId && s.Id == sectionId, cancellationToken)
            ?? throw new NotFoundException(nameof(KbActSection), sectionId);

        var replacement = new KbActSection(tenantId, current.ActId, current.ParentId, current.Number, newTitle ?? current.Title, newBody, amendedOn, null, current.Path);
        current.CloseForAmendment(amendedOn);

        await db.KbActSections.AddAsync(replacement, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(replacement);
    }

    public async Task<IReadOnlyList<KbActSectionDto>> GetSectionHistoryAsync(Guid tenantId, Guid actId, string number, CancellationToken cancellationToken = default)
    {
        var rows = await db.KbActSections.IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId && s.ActId == actId && s.Number == number)
            .OrderBy(s => s.EffectiveFrom)
            .ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    private async Task<KbAct> GetActOrThrowAsync(Guid tenantId, Guid actId, CancellationToken cancellationToken)
        => await db.KbActs.SingleOrDefaultAsync(a => a.TenantId == tenantId && a.Id == actId, cancellationToken)
           ?? throw new NotFoundException(nameof(KbAct), actId);

    private static string BuildPath(KbActSection? parent, Guid sectionId)
        => parent?.Path is { } parentPath ? $"{parentPath}.{sectionId:N}" : sectionId.ToString("N");

    private static bool IsSectionNumberConflict(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("ux_kb_act_sections_act_number", StringComparison.OrdinalIgnoreCase) == true;

    private static KbActDto ToDto(KbAct a) => new(a.Id, a.Name, a.ShortCode, a.Jurisdiction, a.Year);

    private static KbActSectionDto ToDto(KbActSection s) => new(s.Id, s.ActId, s.ParentId, s.Number, s.Title, s.Body, s.EffectiveFrom, s.EffectiveTo, s.Path);
}
