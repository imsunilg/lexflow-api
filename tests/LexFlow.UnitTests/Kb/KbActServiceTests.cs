using FluentAssertions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Infrastructure.Kb;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Kb;

/// <summary>Module 12: Acts/Sections CRUD + AC-KB3 as-on-date historical rendering.</summary>
public sealed class KbActServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task CreateSectionAsync_throws_when_the_number_already_exists_in_the_act()
    {
        await using var db = CreateContext(nameof(CreateSectionAsync_throws_when_the_number_already_exists_in_the_act));
        var service = new KbActService(db);
        var tenantId = Guid.NewGuid();
        var act = await service.CreateActAsync(tenantId, "Indian Penal Code", "IPC", "India", 1860, CancellationToken.None);
        await service.CreateSectionAsync(tenantId, act.Id, null, "420", "Cheating", "Whoever cheats...", null, CancellationToken.None);

        var act2 = () => service.CreateSectionAsync(tenantId, act.Id, null, "420", "Duplicate", "...", null, CancellationToken.None);

        await act2.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "SECTION_NUMBER_NOT_UNIQUE");
    }

    [Fact]
    public async Task LookupSectionAsync_resolves_by_short_code_and_number_AC_KB1()
    {
        await using var db = CreateContext(nameof(LookupSectionAsync_resolves_by_short_code_and_number_AC_KB1));
        var service = new KbActService(db);
        var tenantId = Guid.NewGuid();
        var act = await service.CreateActAsync(tenantId, "Indian Penal Code", "IPC", "India", 1860, CancellationToken.None);
        await service.CreateSectionAsync(tenantId, act.Id, null, "420", "Cheating", "Whoever cheats...", null, CancellationToken.None);

        var result = await service.LookupSectionAsync(tenantId, "IPC", "420", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Cheating");
    }

    [Fact]
    public async Task AmendSectionAsync_closes_the_old_row_and_creates_a_replacement()
    {
        await using var db = CreateContext(nameof(AmendSectionAsync_closes_the_old_row_and_creates_a_replacement));
        var service = new KbActService(db);
        var tenantId = Guid.NewGuid();
        var act = await service.CreateActAsync(tenantId, "Some Act", "SA", "India", 2000, CancellationToken.None);
        var original = await service.CreateSectionAsync(tenantId, act.Id, null, "10", "Original title", "Original body", new DateOnly(2000, 1, 1), CancellationToken.None);

        var amended = await service.AmendSectionAsync(tenantId, original.Id, "Amended title", "Amended body", new DateOnly(2023, 6, 1), CancellationToken.None);

        amended.Id.Should().NotBe(original.Id);
        amended.Number.Should().Be("10");
        amended.EffectiveFrom.Should().Be(new DateOnly(2023, 6, 1));

        // The live GetSectionsAsync listing should now show only the amended (current) row.
        var current = await service.GetSectionsAsync(tenantId, act.Id, CancellationToken.None);
        current.Should().ContainSingle(s => s.Number == "10" && s.Id == amended.Id);
    }

    [Fact]
    public async Task GetSectionAsOfAsync_renders_the_historical_text_before_the_amendment_AC_KB3()
    {
        await using var db = CreateContext(nameof(GetSectionAsOfAsync_renders_the_historical_text_before_the_amendment_AC_KB3));
        var service = new KbActService(db);
        var tenantId = Guid.NewGuid();
        var act = await service.CreateActAsync(tenantId, "Some Act", "SA", "India", 2000, CancellationToken.None);
        await service.CreateSectionAsync(tenantId, act.Id, null, "10", "Original title", "Original body", new DateOnly(2000, 1, 1), CancellationToken.None);
        await service.AmendSectionAsync(tenantId, (await service.LookupSectionAsync(tenantId, "SA", "10", CancellationToken.None))!.Id, "Amended title", "Amended body", new DateOnly(2023, 6, 1), CancellationToken.None);

        var beforeAmendment = await service.GetSectionAsOfAsync(tenantId, act.Id, "10", new DateOnly(2010, 1, 1), CancellationToken.None);
        var afterAmendment = await service.GetSectionAsOfAsync(tenantId, act.Id, "10", new DateOnly(2024, 1, 1), CancellationToken.None);

        beforeAmendment.Should().NotBeNull();
        beforeAmendment!.Title.Should().Be("Original title");
        afterAmendment.Should().NotBeNull();
        afterAmendment!.Title.Should().Be("Amended title");
    }

    [Fact]
    public async Task GetSectionHistoryAsync_returns_every_version_including_soft_deleted_ones()
    {
        await using var db = CreateContext(nameof(GetSectionHistoryAsync_returns_every_version_including_soft_deleted_ones));
        var service = new KbActService(db);
        var tenantId = Guid.NewGuid();
        var act = await service.CreateActAsync(tenantId, "Some Act", "SA", "India", 2000, CancellationToken.None);
        var original = await service.CreateSectionAsync(tenantId, act.Id, null, "10", "V1", "Body 1", new DateOnly(2000, 1, 1), CancellationToken.None);
        await service.AmendSectionAsync(tenantId, original.Id, "V2", "Body 2", new DateOnly(2023, 6, 1), CancellationToken.None);

        var history = await service.GetSectionHistoryAsync(tenantId, act.Id, "10", CancellationToken.None);

        history.Should().HaveCount(2);
        history.Select(s => s.Title).Should().Contain(["V1", "V2"]);
    }
}
