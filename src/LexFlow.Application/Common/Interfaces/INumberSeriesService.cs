namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 15 §10 Number Series CRUD (PRD §17: "CRUD number-series, tax-rates, templates"). AC-S2: preview must match the actual next generated number.</summary>
public interface INumberSeriesService
{
    Task<IReadOnlyList<NumberSeriesDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<NumberSeriesDto> CreateAsync(Guid tenantId, string seriesKey, int fiscalYear, string formatPattern, Guid? branchId, CancellationToken cancellationToken = default);

    Task<NumberSeriesDto> UpdatePatternAsync(Guid tenantId, Guid id, string formatPattern, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Renders the pattern against the series' current next_seq without consuming it (AC-S2).</summary>
    Task<string> PreviewNextAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
}

public sealed record NumberSeriesDto(Guid Id, string SeriesKey, int FiscalYear, string FormatPattern, long NextSeq, Guid? BranchId);
