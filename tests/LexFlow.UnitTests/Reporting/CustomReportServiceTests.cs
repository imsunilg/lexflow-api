using FluentAssertions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using LexFlow.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Reporting;

/// <summary>Module 13 Custom Report Builder: whitelist enforcement (no raw SQL ever) and the filter/group-by/aggregate/scope pipeline.</summary>
public sealed class CustomReportServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private static Matter NewMatter(Guid tenantId, Guid? lawyerId, Guid? branchId, string status, decimal budget)
    {
        var matter = new Matter(tenantId, $"M-{Guid.NewGuid():N}", "Test Matter", Guid.NewGuid(), "Litigation", null, branchId, lawyerId, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, budget, "{}");
        if (status != "Open")
        {
            matter.ChangeStatus(status, null, null);
        }

        return matter;
    }

    [Fact]
    public async Task CreateAsync_throws_for_a_base_entity_outside_the_whitelist()
    {
        await using var db = CreateContext(nameof(CreateAsync_throws_for_a_base_entity_outside_the_whitelist));
        var service = new CustomReportService(db);
        var input = new CustomReportDefinitionInput("Bad", "User", ["Id"], null, [], [], [], "private");

        var act = () => service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), input, CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "BASE_ENTITY_NOT_WHITELISTED");
    }

    [Fact]
    public async Task CreateAsync_throws_for_a_column_not_in_the_whitelisted_field_catalog()
    {
        await using var db = CreateContext(nameof(CreateAsync_throws_for_a_column_not_in_the_whitelisted_field_catalog));
        var service = new CustomReportService(db);
        var input = new CustomReportDefinitionInput("Bad", "Matter", ["Number", "NotAColumn"], null, [], [], [], "private");

        var act = () => service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), input, CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "FIELD_NOT_WHITELISTED");
    }

    [Fact]
    public async Task CreateAsync_throws_for_a_filter_field_not_in_the_whitelist_even_when_columns_are_valid()
    {
        await using var db = CreateContext(nameof(CreateAsync_throws_for_a_filter_field_not_in_the_whitelist_even_when_columns_are_valid));
        var service = new CustomReportService(db);
        var filter = new CustomReportFilterGroup("AND", [new CustomReportFilter("SmuggledRawField", "eq", "x")]);
        var input = new CustomReportDefinitionInput("Bad", "Matter", ["Number"], filter, [], [], [], "private");

        var act = () => service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), input, CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "FIELD_NOT_WHITELISTED");
    }

    [Fact]
    public async Task RunAsync_scope_own_only_returns_the_callers_own_matters()
    {
        await using var db = CreateContext(nameof(RunAsync_scope_own_only_returns_the_callers_own_matters));
        var tenantId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherLawyerId = Guid.NewGuid();

        await db.Matters.AddAsync(NewMatter(tenantId, ownerId, null, "Open", 1000));
        await db.Matters.AddAsync(NewMatter(tenantId, otherLawyerId, null, "Open", 5000));
        await db.SaveChangesAsync();

        var service = new CustomReportService(db);
        var input = new CustomReportDefinitionInput("My Matters", "Matter", ["Number", "ResponsibleLawyerId"], null, [], [], [], "private");
        var definition = await service.CreateAsync(tenantId, ownerId, input, CancellationToken.None);

        var scope = new ReportScope("own", [ownerId], null);
        var result = await service.RunAsync(tenantId, definition.Id, scope, CancellationToken.None);

        result.Rows.Should().ContainSingle();
        result.Rows.Single()[1].Should().Be(ownerId);
    }

    [Fact]
    public async Task RunAsync_applies_filter_group_by_and_sum_aggregate()
    {
        await using var db = CreateContext(nameof(RunAsync_applies_filter_group_by_and_sum_aggregate));
        var tenantId = Guid.NewGuid();
        var lawyerId = Guid.NewGuid();

        await db.Matters.AddAsync(NewMatter(tenantId, lawyerId, null, "Open", 1000));
        await db.Matters.AddAsync(NewMatter(tenantId, lawyerId, null, "Open", 2000));
        await db.Matters.AddAsync(NewMatter(tenantId, lawyerId, null, "Closed", 500));
        await db.SaveChangesAsync();

        var service = new CustomReportService(db);
        var filter = new CustomReportFilterGroup("AND", [new CustomReportFilter("Status", "eq", "Open")]);
        var input = new CustomReportDefinitionInput(
            "Budget by status",
            "Matter",
            [],
            filter,
            ["Status"],
            [new CustomReportAggregate("Budget", "sum", "TotalBudget")],
            [],
            "private");
        var definition = await service.CreateAsync(tenantId, lawyerId, input, CancellationToken.None);

        var scope = new ReportScope("all", null, null);
        var result = await service.RunAsync(tenantId, definition.Id, scope, CancellationToken.None);

        result.Columns.Should().Equal("Status", "TotalBudget");
        result.Rows.Should().ContainSingle();
        result.Rows.Single()[0].Should().Be("Open");
        result.Rows.Single()[1].Should().Be(3000m);
    }
}
