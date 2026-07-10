using FluentAssertions;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using LexFlow.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Reporting;

/// <summary>Module 13 Database: "heavy aggregates from nightly-built star-schema tables ... (refreshed hourly incremental)" — the ETL job that populates rpt.* from the OLTP schemas.</summary>
public sealed class ReportingEtlServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task RunIncrementalAsync_upserts_a_dim_lawyer_row_per_user()
    {
        await using var db = CreateContext(nameof(RunIncrementalAsync_upserts_a_dim_lawyer_row_per_user));
        var tenantId = Guid.NewGuid();
        var user = new User(tenantId, "lawyer@firm.test", "Priya Sharma");
        user.Activate();
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();

        var service = new ReportingEtlService(db);
        await service.RunIncrementalAsync(CancellationToken.None);

        var dim = await db.RptDimLawyers.SingleAsync(l => l.LawyerKey == user.Id);
        dim.Name.Should().Be("Priya Sharma");
        dim.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RunIncrementalAsync_upserts_fact_billing_from_the_owning_invoice_and_matter()
    {
        await using var db = CreateContext(nameof(RunIncrementalAsync_upserts_fact_billing_from_the_owning_invoice_and_matter));
        var tenantId = Guid.NewGuid();
        var lawyerId = Guid.NewGuid();
        var matter = new Matter(tenantId, "M-1", "Matter", Guid.NewGuid(), "Litigation", null, null, lawyerId, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        await db.Matters.AddAsync(matter);

        var invoice = new Invoice(tenantId, matter.Id, Guid.NewGuid(), new DateOnly(2026, 3, 15), null, "INR", null);
        invoice.SetTotals(1000, 0, 180, 1180);
        invoice.ApplyPayment(500);
        await db.Invoices.AddAsync(invoice);
        await db.SaveChangesAsync();

        var service = new ReportingEtlService(db);
        await service.RunIncrementalAsync(CancellationToken.None);

        var fact = await db.RptFactBillings.SingleAsync(f => f.InvoiceId == invoice.Id);
        fact.BilledAmount.Should().Be(1180);
        fact.CollectedAmount.Should().Be(500);
        fact.OutstandingAmount.Should().Be(680);
        fact.LawyerKey.Should().Be(lawyerId);
        fact.DateKey.Should().Be(20260315);
    }

    [Fact]
    public async Task RunIncrementalAsync_upserts_fact_time_from_time_entries()
    {
        await using var db = CreateContext(nameof(RunIncrementalAsync_upserts_fact_time_from_time_entries));
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var matter = new Matter(tenantId, "M-1", "Matter", Guid.NewGuid(), "Litigation", null, null, null, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        await db.Matters.AddAsync(matter);
        var entry = new TimeEntry(tenantId, userId, matter.Id, null, new DateOnly(2026, 4, 1), null, 90, 90, true, "Drafting", null, "manual");
        await db.TimeEntries.AddAsync(entry);
        await db.SaveChangesAsync();

        var service = new ReportingEtlService(db);
        await service.RunIncrementalAsync(CancellationToken.None);

        var fact = await db.RptFactTimes.SingleAsync(f => f.TimeEntryId == entry.Id);
        fact.LawyerKey.Should().Be(userId);
        fact.DurationMin.Should().Be(90);
        fact.IsBilled.Should().BeFalse();
    }

    [Fact]
    public async Task RunIncrementalAsync_is_idempotent_and_reflects_the_latest_matter_status_on_rerun()
    {
        await using var db = CreateContext(nameof(RunIncrementalAsync_is_idempotent_and_reflects_the_latest_matter_status_on_rerun));
        var tenantId = Guid.NewGuid();
        var matter = new Matter(tenantId, "M-1", "Matter", Guid.NewGuid(), "Litigation", null, null, null, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        await db.Matters.AddAsync(matter);
        await db.SaveChangesAsync();

        var service = new ReportingEtlService(db);
        await service.RunIncrementalAsync(CancellationToken.None);

        matter.ChangeStatus("Closed", "Settled", DateOnly.FromDateTime(DateTime.UtcNow));
        await db.SaveChangesAsync();
        await service.RunIncrementalAsync(CancellationToken.None);

        var facts = await db.RptFactMatters.Where(f => f.MatterId == matter.Id).ToListAsync();
        facts.Should().ContainSingle("a rerun must upsert the existing row, not duplicate it");
        facts.Single().Status.Should().Be("Closed");
        facts.Single().IsOpen.Should().BeFalse();
    }
}
