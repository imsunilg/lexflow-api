namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Read-only dropdown data for the matter/case-create forms (courts, judges, practice
/// areas) — none of these have a dedicated CRUD endpoint in Module 4/5's own API list
/// (they're tenant-seeded reference data per PRD §14/DB-14), but the UI needs to list
/// them to populate typeaheads.
/// </summary>
public interface ILegalLookupService
{
    Task<IReadOnlyList<CourtDto>> GetCourtsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JudgeDto>> GetJudgesAsync(Guid tenantId, Guid? courtId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PracticeAreaDto>> GetPracticeAreasAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed record CourtDto(Guid Id, string Name, string Level, string? City, string? State, string? Bench, string Tz);

public sealed record JudgeDto(Guid Id, string Name, Guid CourtId, bool Active);

public sealed record PracticeAreaDto(Guid Id, string Name, Guid? ParentId, bool IsActive);
