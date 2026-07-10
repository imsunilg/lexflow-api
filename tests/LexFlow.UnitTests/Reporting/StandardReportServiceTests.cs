using FluentAssertions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using LexFlow.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Reporting;

/// <summary>Module 13: the 12 standard reports. AC-R1: "every standard report's totals reconcile with module list-view totals under identical filters."</summary>
public sealed class StandardReportServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private static readonly ReportScope AllScope = new("all", null, null);

    [Fact]
    public void GetCatalog_lists_all_12_PRD_standard_reports()
    {
        var service = new StandardReportService(CreateContext(nameof(GetCatalog_lists_all_12_PRD_standard_reports)));

        service.GetCatalog().Should().HaveCount(12);
    }

    [Fact]
    public async Task RunAsync_billing_report_totals_reconcile_with_the_underlying_invoices_AC_R1()
    {
        await using var db = CreateContext(nameof(RunAsync_billing_report_totals_reconcile_with_the_underlying_invoices_AC_R1));
        var tenantId = Guid.NewGuid();
        var matter = new Matter(tenantId, "M-1", "Matter", Guid.NewGuid(), "Litigation", null, null, null, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        await db.Matters.AddAsync(matter);

        var invoice1 = new Invoice(tenantId, matter.Id, Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), null, "INR", null);
        invoice1.SetTotals(1000, 0, 180, 1180);
        invoice1.ApplyPayment(1180);
        var invoice2 = new Invoice(tenantId, matter.Id, Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), null, "INR", null);
        invoice2.SetTotals(500, 0, 90, 590);

        await db.Invoices.AddRangeAsync(invoice1, invoice2);
        await db.SaveChangesAsync();

        var service = new StandardReportService(db);
        var reportParams = new ReportRunParams(null, null, null, null, null, null);
        var result = await service.RunAsync(tenantId, "billing", reportParams, AllScope, CancellationToken.None);

        var totalBilledInReport = result.Rows.Sum(r => (decimal)r[2]!);
        var totalPaidInReport = result.Rows.Sum(r => (decimal)r[3]!);
        var expectedBilled = await db.Invoices.Where(i => i.TenantId == tenantId).SumAsync(i => i.GrandTotal);
        var expectedPaid = await db.Invoices.Where(i => i.TenantId == tenantId).SumAsync(i => i.AmountPaid);

        totalBilledInReport.Should().Be(expectedBilled);
        totalPaidInReport.Should().Be(expectedPaid);
    }

    [Fact]
    public async Task RunAsync_revenue_report_reconciles_with_the_star_schema_fact_totals_AC_R1()
    {
        await using var db = CreateContext(nameof(RunAsync_revenue_report_reconciles_with_the_star_schema_fact_totals_AC_R1));
        var tenantId = Guid.NewGuid();

        await db.RptFactBillings.AddAsync(new RptFactBilling(tenantId, Guid.NewGuid(), 20260115, null, null, null, null, 1000, 800, 0, 200, 180));
        await db.RptFactBillings.AddAsync(new RptFactBilling(tenantId, Guid.NewGuid(), 20260220, null, null, null, null, 500, 500, 0, 0, 90));
        await db.SaveChangesAsync();

        var service = new StandardReportService(db);
        var reportParams = new ReportRunParams(null, null, null, null, null, null);
        var result = await service.RunAsync(tenantId, "revenue", reportParams, AllScope, CancellationToken.None);

        var totalBilled = result.Rows.Sum(r => (decimal)r[1]!);
        var expected = await db.RptFactBillings.Where(f => f.TenantId == tenantId).SumAsync(f => f.BilledAmount);

        totalBilled.Should().Be(expected);
        result.Rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task RunAsync_applies_own_scope_to_the_lawyer_performance_report()
    {
        await using var db = CreateContext(nameof(RunAsync_applies_own_scope_to_the_lawyer_performance_report));
        var tenantId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherLawyerId = Guid.NewGuid();

        await db.RptFactTimes.AddAsync(new RptFactTime(tenantId, Guid.NewGuid(), 20260101, ownerId, null, null, null, Guid.NewGuid(), 120, 120, true, false, 1000));
        await db.RptFactTimes.AddAsync(new RptFactTime(tenantId, Guid.NewGuid(), 20260101, otherLawyerId, null, null, null, Guid.NewGuid(), 60, 60, true, false, 500));
        await db.SaveChangesAsync();

        var service = new StandardReportService(db);
        var scope = new ReportScope("own", [ownerId], null);
        var result = await service.RunAsync(tenantId, "lawyer-performance", new ReportRunParams(null, null, null, null, null, null), scope, CancellationToken.None);

        result.Rows.Should().ContainSingle();
        result.Rows.Single()[0].Should().Be(ownerId);
    }

    [Fact]
    public async Task RunAsync_throws_when_the_date_range_exceeds_three_years()
    {
        await using var db = CreateContext(nameof(RunAsync_throws_when_the_date_range_exceeds_three_years));
        var service = new StandardReportService(db);
        var reportParams = new ReportRunParams(new DateOnly(2020, 1, 1), new DateOnly(2026, 1, 1), null, null, null, null);

        var act = () => service.RunAsync(Guid.NewGuid(), "billing", reportParams, AllScope, CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "DATE_RANGE_TOO_LARGE");
    }
}
