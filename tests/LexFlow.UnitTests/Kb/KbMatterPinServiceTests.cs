using FluentAssertions;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Kb;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Kb;

/// <summary>Module 12 pin-to-matter. AC-KB4/edge case: "orphan pins after KB item unpublish (pin retains snapshot text)".</summary>
public sealed class KbMatterPinServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task PinAsync_snapshots_the_judgments_headnote_at_pin_time()
    {
        await using var db = CreateContext(nameof(PinAsync_snapshots_the_judgments_headnote_at_pin_time));
        var service = new KbMatterPinService(db);
        var tenantId = Guid.NewGuid();
        var matterId = Guid.NewGuid();
        var judgment = new KbJudgment(tenantId, "AIR 2020 SC 1", null, null, null, "A v. B", "Original headnote", null);
        await db.KbJudgments.AddAsync(judgment);
        await db.SaveChangesAsync();

        var pin = await service.PinAsync(tenantId, matterId, Guid.NewGuid(), "Judgment", judgment.Id, "cite for limitation point", CancellationToken.None);

        pin.SnapshotText.Should().Be("Original headnote");
        pin.Note.Should().Be("cite for limitation point");
    }

    [Fact]
    public async Task PinAsync_survives_the_source_judgment_later_being_edited_orphan_pin_edge_case()
    {
        await using var db = CreateContext(nameof(PinAsync_survives_the_source_judgment_later_being_edited_orphan_pin_edge_case));
        var service = new KbMatterPinService(db);
        var tenantId = Guid.NewGuid();
        var matterId = Guid.NewGuid();
        var judgment = new KbJudgment(tenantId, "AIR 2021 SC 2", null, null, null, "C v. D", "Headnote v1", null);
        await db.KbJudgments.AddAsync(judgment);
        await db.SaveChangesAsync();

        var pin = await service.PinAsync(tenantId, matterId, Guid.NewGuid(), "Judgment", judgment.Id, null, CancellationToken.None);

        // The source judgment is edited after the pin was created.
        judgment.UpdateHeadnote("Headnote v2, substantially rewritten");
        await db.SaveChangesAsync();

        var pins = await service.GetForMatterAsync(tenantId, matterId, CancellationToken.None);

        pins.Single(p => p.Id == pin.Id).SnapshotText.Should().Be("Headnote v1", "the pin must retain what the judgment said at pin time, not the live (later-edited) text");
    }

    [Fact]
    public async Task GetPinCountAsync_returns_the_number_of_distinct_matters_the_item_is_pinned_into_AC_KB4()
    {
        await using var db = CreateContext(nameof(GetPinCountAsync_returns_the_number_of_distinct_matters_the_item_is_pinned_into_AC_KB4));
        var service = new KbMatterPinService(db);
        var tenantId = Guid.NewGuid();
        var judgment = new KbJudgment(tenantId, "AIR 2022 SC 3", null, null, null, "E v. F", "Headnote", null);
        await db.KbJudgments.AddAsync(judgment);
        await db.SaveChangesAsync();

        await service.PinAsync(tenantId, Guid.NewGuid(), Guid.NewGuid(), "Judgment", judgment.Id, null, CancellationToken.None);
        await service.PinAsync(tenantId, Guid.NewGuid(), Guid.NewGuid(), "Judgment", judgment.Id, null, CancellationToken.None);
        await service.PinAsync(tenantId, Guid.NewGuid(), Guid.NewGuid(), "Judgment", judgment.Id, null, CancellationToken.None);

        var count = await service.GetPinCountAsync(tenantId, "Judgment", judgment.Id, CancellationToken.None);

        count.Should().Be(3);
    }

    [Fact]
    public async Task UnpinAsync_removes_the_pin()
    {
        await using var db = CreateContext(nameof(UnpinAsync_removes_the_pin));
        var service = new KbMatterPinService(db);
        var tenantId = Guid.NewGuid();
        var matterId = Guid.NewGuid();
        var judgment = new KbJudgment(tenantId, "AIR 2023 SC 4", null, null, null, "G v. H", "Headnote", null);
        await db.KbJudgments.AddAsync(judgment);
        await db.SaveChangesAsync();
        var pin = await service.PinAsync(tenantId, matterId, Guid.NewGuid(), "Judgment", judgment.Id, null, CancellationToken.None);

        await service.UnpinAsync(tenantId, pin.Id, CancellationToken.None);

        (await service.GetForMatterAsync(tenantId, matterId, CancellationToken.None)).Should().BeEmpty();
    }
}
