namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 12: Acts (hierarchy: Act -&gt; Chapter -&gt; Section -&gt; sub-section text) with as-on-date
/// historical rendering. AC-KB3: amending a section never rewrites its old text in place — <see
/// cref="AmendSectionAsync"/> closes the current row's validity window and inserts a fresh
/// replacement, so <see cref="GetSectionAsOfAsync"/> can always resolve exactly what the law read
/// on any given date.
/// </summary>
public interface IKbActService
{
    Task<KbActDto> CreateActAsync(Guid tenantId, string name, string? shortCode, string? jurisdiction, int? year, CancellationToken cancellationToken = default);

    Task<KbActDto> UpdateActAsync(Guid tenantId, Guid actId, string name, string? shortCode, string? jurisdiction, int? year, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KbActDto>> GetActsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<KbActDto?> GetActAsync(Guid tenantId, Guid actId, CancellationToken cancellationToken = default);

    Task<KbActSectionDto> CreateSectionAsync(Guid tenantId, Guid actId, Guid? parentId, string number, string? title, string? body, DateOnly? effectiveFrom, CancellationToken cancellationToken = default);

    /// <summary>Module 12 Validation Rules: "section numbers unique within act" (DB-enforced, translated to DomainRuleException("SECTION_NUMBER_NOT_UNIQUE", ...) on conflict).</summary>
    Task<IReadOnlyList<KbActSectionDto>> GetSectionsAsync(Guid tenantId, Guid actId, CancellationToken cancellationToken = default);

    Task<KbActSectionDto?> GetSectionAsync(Guid tenantId, Guid sectionId, CancellationToken cancellationToken = default);

    /// <summary>AC-KB1: "IPC 420" -&gt; direct section jump. Resolves the currently-active (effective_to IS NULL or in the future) row for (act short code or name, number).</summary>
    Task<KbActSectionDto?> LookupSectionAsync(Guid tenantId, string actShortCodeOrName, string number, CancellationToken cancellationToken = default);

    /// <summary>AC-KB3: as-on-date view of an amended section renders historical text correctly — resolved across both live and soft-deleted (historical) rows for the same (act, number).</summary>
    Task<KbActSectionDto?> GetSectionAsOfAsync(Guid tenantId, Guid actId, string number, DateOnly asOfDate, CancellationToken cancellationToken = default);

    /// <summary>Module 12 edge case: "Act amendments (section versions with effective dates; reader shows as-on-date selector)". Closes the current row's validity at amendedOn and creates the replacement in one operation.</summary>
    Task<KbActSectionDto> AmendSectionAsync(Guid tenantId, Guid sectionId, string? newTitle, string? newBody, DateOnly amendedOn, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KbActSectionDto>> GetSectionHistoryAsync(Guid tenantId, Guid actId, string number, CancellationToken cancellationToken = default);
}

public sealed record KbActDto(Guid Id, string Name, string? ShortCode, string? Jurisdiction, int? Year);

public sealed record KbActSectionDto(Guid Id, Guid ActId, Guid? ParentId, string Number, string? Title, string? Body, DateOnly? EffectiveFrom, DateOnly? EffectiveTo, string? Path);
