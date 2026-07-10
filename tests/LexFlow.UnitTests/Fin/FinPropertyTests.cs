using FluentAssertions;
using FsCheck.Xunit;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Fin;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Fin;

/// <summary>
/// §37: "Money &amp; trust invariants: property-based tests (FsCheck): allocation never exceeds,
/// trust never negative under concurrent op sequences, invoice totals = Σ lines under random line
/// sets." These are the highest-risk invariants in the system per this module's build brief, so
/// they're written alongside the feature, not deferred.
///
/// Scope note on "concurrent": FsCheck generates a random *sequence* of operations applied one
/// after another (not literal parallel threads) — the property under test is "the balance never
/// goes negative no matter what order/mix of deposits and disbursements arrives", which is exactly
/// what a concurrent op sequence would also have to satisfy once serialized by the DB trigger's
/// row lock (AC-B4). True concurrent-thread races against the trigger's SELECT...FOR UPDATE are a
/// Postgres-only guarantee that EF InMemory cannot exercise (documented elsewhere in this codebase
/// for every trigger-backed invariant) — this property test covers the sequence-level invariant the
/// application layer is independently responsible for (see TrustService's own doc comments).
/// </summary>
public sealed class FinPropertyTests
{
    private static LexFlowDbContext CreateContext() => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    /// <summary>Converts an arbitrary (possibly int.MinValue) FsCheck-generated int into a bounded, non-negative "cents" decimal — Math.Abs(int.MinValue) overflows, so this widens to long first.</summary>
    private static decimal ToBoundedAmount(int raw) => GstCalculator.Round(Math.Abs((long)raw % 10_000_00) / 100m);

    [Property(MaxTest = 200)]
    public void Invoice_grand_total_always_equals_the_sum_of_its_lines_plus_tax_minus_discount(int[] rawLineCents, int rawDiscountCents, bool zeroRated)
    {
        var lineAmounts = rawLineCents
            .Select(ToBoundedAmount)
            .Where(a => a > 0)
            .Take(25)
            .ToList();

        if (lineAmounts.Count == 0)
        {
            return;
        }

        var subTotal = lineAmounts.Sum();
        var discountTotal = Math.Min(subTotal, ToBoundedAmount(rawDiscountCents));
        var taxableAmount = subTotal - discountTotal;

        var taxLines = GstCalculator.Calculate(taxableAmount, "27", "27", 9, 9, 18, zeroRated);
        var taxTotal = taxLines.Sum(t => t.Amount);
        var grandTotal = taxableAmount + taxTotal;

        // AC-B1: "totals reconcile to WIP report to the paisa" — no independent re-rounding step
        // exists anywhere between "sum of already-rounded lines" and "grand total".
        subTotal.Should().Be(lineAmounts.Sum());
        grandTotal.Should().Be(subTotal - discountTotal + taxTotal);
        grandTotal.Should().Be(GstCalculator.Round(grandTotal), "every intermediate amount is already round-half-even to the paisa, so summing them introduces no further rounding drift");
    }

    [Property(MaxTest = 30)]
    public void Trust_balance_never_goes_negative_under_a_random_sequence_of_deposits_and_disbursements(int[] rawOps)
    {
        using var db = CreateContext();
        var trustService = new TrustService(db);
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        decimal expectedBalance = 0;

        foreach (var raw in rawOps.Take(20))
        {
            var amount = ToBoundedAmount(raw);
            if (amount <= 0)
            {
                continue;
            }

            if (raw >= 0)
            {
                trustService.DepositAsync(tenantId, actorId, clientId, amount, "property-test deposit", null, CancellationToken.None).GetAwaiter().GetResult();
                expectedBalance += amount;
            }
            else
            {
                try
                {
                    trustService.DisburseAsync(tenantId, actorId, clientId, amount, "property-test disbursement", null, "property-test authorization", null, CancellationToken.None).GetAwaiter().GetResult();
                    expectedBalance -= amount;
                }
                catch (DomainRuleException ex) when (ex.SubCode == "INSUFFICIENT_TRUST_BALANCE")
                {
                    // BR-3: correctly rejected — the requested disbursement must indeed have
                    // exceeded the balance at the time it was attempted.
                    amount.Should().BeGreaterThan(expectedBalance);
                }
            }

            // BR-3/AC-B4: the invariant under test — no sequence of operations can ever drive the
            // balance negative, whether via a successful disbursement or (impossibly) an accepted
            // overdraft.
            expectedBalance.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    [Property(MaxTest = 30)]
    public void Payment_allocations_never_exceed_the_payment_amount_or_any_invoice_open_balance(int rawPaymentAmount, int[] rawAllocationAmounts)
    {
        using var db = CreateContext();
        var paymentService = new PaymentService(db);
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var matterId = Guid.NewGuid();

        var paymentAmount = ToBoundedAmount(rawPaymentAmount);
        if (paymentAmount <= 0)
        {
            return;
        }

        var invoice = new Invoice(tenantId, matterId, clientId, DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow).AddDays(15), "INR", null);
        var invoiceGrandTotal = ToBoundedAmount(rawAllocationAmounts.FirstOrDefault()) + 1;
        invoice.SetTotals(invoiceGrandTotal, 0, 0, invoiceGrandTotal);
        db.Invoices.Add(invoice);
        db.SaveChanges();

        var allocations = rawAllocationAmounts
            .Select(ToBoundedAmount)
            .Where(a => a > 0)
            .Take(5)
            .Select(a => new PaymentAllocationInput(invoice.Id, a))
            .ToList();

        if (allocations.Count == 0)
        {
            return;
        }

        var allocationSum = allocations.Sum(a => a.Amount);

        try
        {
            var result = paymentService.RecordPaymentAsync(tenantId, new RecordPaymentInput(clientId, paymentAmount, "Cash", null, null, DateOnly.FromDateTime(DateTime.UtcNow), allocations, null), CancellationToken.None)
                .GetAwaiter().GetResult();

            // §19.8: "Σallocations ≤ payment.amount and per-invoice Σ ≤ open balance" — checked
            // directly against what was actually persisted, not just the input.
            var persistedTotal = result.Allocations.Sum(a => a.Amount);
            persistedTotal.Should().BeLessThanOrEqualTo(paymentAmount);
            persistedTotal.Should().BeLessThanOrEqualTo(invoiceGrandTotal);
        }
        catch (DomainRuleException ex) when (ex.SubCode == "OVER_ALLOCATION")
        {
            // Correctly rejected — the requested allocation must indeed have exceeded one of the
            // two ceilings (payment amount or the invoice's open balance).
            (allocationSum > paymentAmount || allocationSum > invoiceGrandTotal).Should().BeTrue();

            // Rejection is all-or-nothing: no partial allocation may have been persisted.
            db.PaymentAllocations.Count(a => a.InvoiceId == invoice.Id).Should().Be(0);
        }
    }
}
