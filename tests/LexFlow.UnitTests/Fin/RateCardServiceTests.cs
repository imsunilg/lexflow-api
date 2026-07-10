using FluentAssertions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Fin;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Fin;

/// <summary>BR-7: rate resolution order — entry override -&gt; matter-member override -&gt; matter rate card -&gt; firm default.</summary>
public sealed class RateCardServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task ResolveRateAsync_prefers_a_manual_override_above_everything_else()
    {
        await using var db = CreateContext(nameof(ResolveRateAsync_prefers_a_manual_override_above_everything_else));
        var service = new RateCardService(db);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var matterId = Guid.NewGuid();

        var defaultCard = await service.CreateRateCardAsync(tenantId, "Firm Default", null, isDefault: true, CancellationToken.None);
        await service.UpsertRateCardEntryAsync(tenantId, defaultCard.Id, null, userId, 5000, "INR", DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);

        var resolved = await service.ResolveRateAsync(tenantId, matterId, userId, null, manualOverride: 9999, CancellationToken.None);

        resolved.Should().Be(9999);
    }

    [Fact]
    public async Task ResolveRateAsync_prefers_a_matter_member_override_over_the_firm_default()
    {
        await using var db = CreateContext(nameof(ResolveRateAsync_prefers_a_matter_member_override_over_the_firm_default));
        var service = new RateCardService(db);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var matterId = Guid.NewGuid();

        var defaultCard = await service.CreateRateCardAsync(tenantId, "Firm Default", null, isDefault: true, CancellationToken.None);
        await service.UpsertRateCardEntryAsync(tenantId, defaultCard.Id, null, userId, 5000, "INR", DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);

        var matterCard = await service.CreateRateCardAsync(tenantId, "Matter Card", null, isDefault: false, CancellationToken.None);
        await service.UpsertRateCardEntryAsync(tenantId, matterCard.Id, null, userId, 7500, "INR", DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);
        await service.SetBillingArrangementAsync(tenantId, matterId, new SetBillingArrangementInput("Hourly", matterCard.Id, null, null, null, null, null, null, null), CancellationToken.None);

        var resolved = await service.ResolveRateAsync(tenantId, matterId, userId, null, null, CancellationToken.None);

        resolved.Should().Be(7500);
    }

    [Fact]
    public async Task ResolveRateAsync_falls_back_to_the_firm_default_rate_card_when_the_matter_card_has_no_matching_entry()
    {
        await using var db = CreateContext(nameof(ResolveRateAsync_falls_back_to_the_firm_default_rate_card_when_the_matter_card_has_no_matching_entry));
        var service = new RateCardService(db);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var matterId = Guid.NewGuid();

        var defaultCard = await service.CreateRateCardAsync(tenantId, "Firm Default", null, isDefault: true, CancellationToken.None);
        await service.UpsertRateCardEntryAsync(tenantId, defaultCard.Id, "Associate", null, 4000, "INR", DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);

        var matterCard = await service.CreateRateCardAsync(tenantId, "Matter Card (no matching entry)", null, isDefault: false, CancellationToken.None);
        await service.SetBillingArrangementAsync(tenantId, matterId, new SetBillingArrangementInput("Hourly", matterCard.Id, null, null, null, null, null, null, null), CancellationToken.None);

        var resolved = await service.ResolveRateAsync(tenantId, matterId, userId, "Associate", null, CancellationToken.None);

        resolved.Should().Be(4000);
    }

    [Fact]
    public async Task ResolveRateAsync_throws_when_no_rate_can_be_resolved()
    {
        await using var db = CreateContext(nameof(ResolveRateAsync_throws_when_no_rate_can_be_resolved));
        var service = new RateCardService(db);
        var tenantId = Guid.NewGuid();

        var act = () => service.ResolveRateAsync(tenantId, Guid.NewGuid(), Guid.NewGuid(), null, null, CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "RATE_NOT_CONFIGURED");
    }

    [Fact]
    public async Task SetBillingArrangementAsync_deactivates_the_previous_arrangement_for_the_same_matter()
    {
        await using var db = CreateContext(nameof(SetBillingArrangementAsync_deactivates_the_previous_arrangement_for_the_same_matter));
        var service = new RateCardService(db);
        var tenantId = Guid.NewGuid();
        var matterId = Guid.NewGuid();

        await service.SetBillingArrangementAsync(tenantId, matterId, new SetBillingArrangementInput("Hourly", null, null, null, null, null, null, null, null), CancellationToken.None);
        var second = await service.SetBillingArrangementAsync(tenantId, matterId, new SetBillingArrangementInput("Fixed", null, 50000, null, null, null, null, null, null), CancellationToken.None);

        var current = await service.GetBillingArrangementAsync(tenantId, matterId, CancellationToken.None);
        current!.Id.Should().Be(second.Id);
        current.ArrangementType.Should().Be("Fixed");
    }
}
