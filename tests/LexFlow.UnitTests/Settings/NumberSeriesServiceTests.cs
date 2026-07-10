using FluentAssertions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Infrastructure.Persistence;
using LexFlow.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Settings;

/// <summary>AC-S2: "number preview matches actual next generated number" — and the {seq} token requirement (PRD Module 15 Validation).</summary>
public sealed class NumberSeriesServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task CreateAsync_rejects_a_pattern_without_a_seq_token()
    {
        await using var db = CreateContext(nameof(CreateAsync_rejects_a_pattern_without_a_seq_token));
        var service = new NumberSeriesService(db);

        var act = () => service.CreateAsync(Guid.NewGuid(), "INV", 2026, "{SERIES}-{BR}-{FY}", null);

        await act.Should().ThrowAsync<Application.Common.Exceptions.ValidationException>();
    }

    [Fact]
    public async Task PreviewNextAsync_matches_the_series_current_next_seq_rendered_through_the_pattern()
    {
        await using var db = CreateContext(nameof(PreviewNextAsync_matches_the_series_current_next_seq_rendered_through_the_pattern));
        var service = new NumberSeriesService(db);
        var tenantId = Guid.NewGuid();

        var series = await service.CreateAsync(tenantId, "INV", 2026, "{SERIES}-{FY}-{SEQ:5}", null);

        var preview = await service.PreviewNextAsync(tenantId, series.Id, CancellationToken.None);

        preview.Should().Be("INV-2026-00001");

        // AC-S2: previewing again without any consuming write must return the exact
        // same value — the preview must never itself advance next_seq.
        var previewAgain = await service.PreviewNextAsync(tenantId, series.Id, CancellationToken.None);
        previewAgain.Should().Be(preview);
    }

    [Fact]
    public async Task UpdatePatternAsync_changes_the_pattern_without_resetting_next_seq()
    {
        await using var db = CreateContext(nameof(UpdatePatternAsync_changes_the_pattern_without_resetting_next_seq));
        var service = new NumberSeriesService(db);
        var tenantId = Guid.NewGuid();

        var series = await service.CreateAsync(tenantId, "MAT", 2026, "{SERIES}-{FY}-{SEQ:4}", null);
        var updated = await service.UpdatePatternAsync(tenantId, series.Id, "{SERIES}/{FY}/{SEQ:4}");

        updated.NextSeq.Should().Be(series.NextSeq);
        updated.FormatPattern.Should().Be("{SERIES}/{FY}/{SEQ:4}");
    }
}
