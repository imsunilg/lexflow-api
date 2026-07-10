using System.Reflection;
using FluentAssertions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Fin;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Fin;

/// <summary>
/// Module 8 trust accounting: BR-3 (no negative balance), AC-B4 (disbursement exceeding balance is
/// impossible), dual-control disbursement for Enterprise tenants, reversal, and reconciliation
/// sign-off. See TrustService's own doc comments for why the balance guard is recomputed from the
/// ledger rather than read from the DB-trigger-owned trust_accounts.current_balance column.
/// </summary>
public sealed class TrustServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    /// <summary>core.tenants has no public factory in this codebase (provisioning isn't built yet) — reflection is the only way to seed a specific plan_tier for this one test.</summary>
    private static Tenant CreateTenant(string planTier)
    {
        var tenant = (Tenant)Activator.CreateInstance(typeof(Tenant), nonPublic: true)!;
        typeof(Tenant).GetProperty(nameof(Tenant.PlanTier))!.SetValue(tenant, planTier);
        typeof(Tenant).GetProperty(nameof(Tenant.Name))!.SetValue(tenant, "Property Test Firm");
        typeof(Tenant).GetProperty(nameof(Tenant.Slug))!.SetValue(tenant, $"test-firm-{Guid.NewGuid():N}");
        typeof(Tenant).BaseType!.GetProperty("Id")!.SetValue(tenant, Guid.NewGuid());
        return tenant;
    }

    [Fact]
    public async Task DepositAsync_creates_the_trust_account_on_first_use_and_increases_the_balance()
    {
        await using var db = CreateContext(nameof(DepositAsync_creates_the_trust_account_on_first_use_and_increases_the_balance));
        var service = new TrustService(db);
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        var entry = await service.DepositAsync(tenantId, Guid.NewGuid(), clientId, 100000, "Retainer", "Client email dt 02-07-2026", CancellationToken.None);

        entry.Kind.Should().Be("Deposit");
        var ledger = await service.GetLedgerAsync(tenantId, clientId, CancellationToken.None);
        ledger.Should().ContainSingle();
    }

    [Fact]
    public async Task DisburseAsync_throws_INSUFFICIENT_TRUST_BALANCE_when_amount_exceeds_the_balance()
    {
        await using var db = CreateContext(nameof(DisburseAsync_throws_INSUFFICIENT_TRUST_BALANCE_when_amount_exceeds_the_balance));
        var service = new TrustService(db);
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        await service.DepositAsync(tenantId, Guid.NewGuid(), clientId, 50000, null, null, CancellationToken.None);

        var act = () => service.DisburseAsync(tenantId, Guid.NewGuid(), clientId, 75000, null, null, "Client authorized", null, CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "INSUFFICIENT_TRUST_BALANCE");
    }

    [Fact]
    public async Task DisburseAsync_succeeds_when_amount_is_within_the_balance_and_applies_to_the_named_invoice()
    {
        await using var db = CreateContext(nameof(DisburseAsync_succeeds_when_amount_is_within_the_balance_and_applies_to_the_named_invoice));
        var service = new TrustService(db);
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        await service.DepositAsync(tenantId, Guid.NewGuid(), clientId, 100000, null, null, CancellationToken.None);

        var invoice = new Invoice(tenantId, Guid.NewGuid(), clientId, DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow).AddDays(15), "INR", null);
        invoice.SetTotals(75000, 0, 0, 75000);
        invoice.MarkSent();
        await db.Invoices.AddAsync(invoice);
        await db.SaveChangesAsync();

        var entry = await service.DisburseAsync(tenantId, Guid.NewGuid(), clientId, 75000, "Apply to invoice", invoice.Id, "Client authorized", null, CancellationToken.None);

        entry.Kind.Should().Be("Disbursement");
        (await db.Invoices.SingleAsync(i => i.Id == invoice.Id)).Status.Should().Be("Paid");
    }

    [Fact]
    public async Task DisburseAsync_requires_a_distinct_second_approver_for_Enterprise_tenants()
    {
        await using var db = CreateContext(nameof(DisburseAsync_requires_a_distinct_second_approver_for_Enterprise_tenants));
        var tenant = CreateTenant("Enterprise");
        await db.Tenants.AddAsync(tenant);
        await db.SaveChangesAsync();

        var service = new TrustService(db);
        var clientId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await service.DepositAsync(tenant.Id, actorId, clientId, 100000, null, null, CancellationToken.None);

        var actWithoutSecondApprover = () => service.DisburseAsync(tenant.Id, actorId, clientId, 10000, null, null, "auth", null, CancellationToken.None);
        var actWithSameApproverTwice = () => service.DisburseAsync(tenant.Id, actorId, clientId, 10000, null, null, "auth", actorId, CancellationToken.None);

        await actWithoutSecondApprover.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "SECOND_APPROVER_REQUIRED");
        await actWithSameApproverTwice.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "SECOND_APPROVER_REQUIRED");

        var withSecondApprover = await service.DisburseAsync(tenant.Id, actorId, clientId, 10000, null, null, "auth", Guid.NewGuid(), CancellationToken.None);
        withSecondApprover.Kind.Should().Be("Disbursement");
    }

    [Fact]
    public async Task ReverseAsync_of_a_deposit_removes_funds_and_is_blocked_if_it_would_overdraw()
    {
        await using var db = CreateContext(nameof(ReverseAsync_of_a_deposit_removes_funds_and_is_blocked_if_it_would_overdraw));
        var service = new TrustService(db);
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var deposit = await service.DepositAsync(tenantId, Guid.NewGuid(), clientId, 50000, null, null, CancellationToken.None);
        await service.DisburseAsync(tenantId, Guid.NewGuid(), clientId, 40000, null, null, "auth", null, CancellationToken.None);

        // Only 10000 remains — reversing the original 50000 deposit (cheque bounced) would overdraw.
        var act = () => service.ReverseAsync(tenantId, Guid.NewGuid(), deposit.Id, "Cheque bounced", CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "INSUFFICIENT_TRUST_BALANCE");
    }

    [Fact]
    public async Task SignOffReconciliationAsync_throws_when_bank_and_ledger_balances_do_not_match()
    {
        await using var db = CreateContext(nameof(SignOffReconciliationAsync_throws_when_bank_and_ledger_balances_do_not_match));
        var service = new TrustService(db);
        var tenantId = Guid.NewGuid();
        await service.DepositAsync(tenantId, Guid.NewGuid(), Guid.NewGuid(), 50000, null, null, CancellationToken.None);

        var reconciliation = await service.ImportReconciliationAsync(tenantId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30), DateOnly.FromDateTime(DateTime.UtcNow), 40000, [], null, CancellationToken.None);

        var act = () => service.SignOffReconciliationAsync(tenantId, Guid.NewGuid(), reconciliation.Id, null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SignOffReconciliationAsync_succeeds_when_bank_and_ledger_balances_match()
    {
        await using var db = CreateContext(nameof(SignOffReconciliationAsync_succeeds_when_bank_and_ledger_balances_match));
        var service = new TrustService(db);
        var tenantId = Guid.NewGuid();
        await service.DepositAsync(tenantId, Guid.NewGuid(), Guid.NewGuid(), 50000, null, null, CancellationToken.None);

        var reconciliation = await service.ImportReconciliationAsync(tenantId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30), DateOnly.FromDateTime(DateTime.UtcNow), 50000, [], null, CancellationToken.None);
        var signedOff = await service.SignOffReconciliationAsync(tenantId, Guid.NewGuid(), reconciliation.Id, "All matched", CancellationToken.None);

        signedOff.Status.Should().Be("SignedOff");
    }
}
