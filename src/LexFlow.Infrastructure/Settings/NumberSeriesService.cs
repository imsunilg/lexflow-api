using System.Globalization;
using System.Text.RegularExpressions;
using FluentValidation.Results;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Settings;

/// <summary>Module 15 §10 Number Series CRUD. AC-S2: PreviewNextAsync must match the actual next generated number — both read the same next_seq without consuming it.</summary>
public sealed partial class NumberSeriesService(LexFlowDbContext db) : INumberSeriesService
{
    public async Task<IReadOnlyList<NumberSeriesDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var series = await db.NumberSeries.Where(s => s.TenantId == tenantId).OrderBy(s => s.SeriesKey).ToListAsync(cancellationToken);
        return series.Select(ToDto).ToList();
    }

    public async Task<NumberSeriesDto> CreateAsync(Guid tenantId, string seriesKey, int fiscalYear, string formatPattern, Guid? branchId, CancellationToken cancellationToken = default)
    {
        EnsurePatternContainsSeq(formatPattern);

        var series = new Domain.Entities.NumberSeries(tenantId, seriesKey, fiscalYear, formatPattern, branchId);
        await db.NumberSeries.AddAsync(series, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(series);
    }

    public async Task<NumberSeriesDto> UpdatePatternAsync(Guid tenantId, Guid id, string formatPattern, CancellationToken cancellationToken = default)
    {
        EnsurePatternContainsSeq(formatPattern);

        var series = await db.NumberSeries.SingleOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.NumberSeries), id);

        // Pattern changes apply only to future numbers (PRD Module 15 Edge Cases) —
        // next_seq/history are untouched by UpdatePattern.
        series.UpdatePattern(formatPattern);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(series);
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        var series = await db.NumberSeries.SingleOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.NumberSeries), id);

        db.NumberSeries.Remove(series);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> PreviewNextAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        var series = await db.NumberSeries.SingleOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.NumberSeries), id);

        return Render(series, series.PeekNext());
    }

    private static void EnsurePatternContainsSeq(string formatPattern)
    {
        if (!formatPattern.Contains("{seq", StringComparison.OrdinalIgnoreCase))
        {
            throw new Application.Common.Exceptions.ValidationException(
            [
                new ValidationFailure("formatPattern", "Number series pattern must contain a {seq} token (PRD Module 15 Validation)."),
            ]);
        }
    }

    private static string Render(Domain.Entities.NumberSeries series, long seqValue)
    {
        var rendered = SeqTokenRegex().Replace(series.FormatPattern, match =>
        {
            var width = match.Groups["width"].Success ? int.Parse(match.Groups["width"].Value, CultureInfo.InvariantCulture) : 1;
            return seqValue.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0');
        });

        rendered = rendered
            .Replace("{SERIES}", series.SeriesKey, StringComparison.OrdinalIgnoreCase)
            .Replace("{FY}", series.FiscalYear.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{BR}", series.BranchId?.ToString("N")[..6] ?? "HQ", StringComparison.OrdinalIgnoreCase);

        return rendered;
    }

    [GeneratedRegex(@"\{SEQ(:(?<width>\d+))?\}", RegexOptions.IgnoreCase)]
    private static partial Regex SeqTokenRegex();

    private static NumberSeriesDto ToDto(Domain.Entities.NumberSeries series) => new(series.Id, series.SeriesKey, series.FiscalYear, series.FormatPattern, series.NextSeq, series.BranchId);
}
