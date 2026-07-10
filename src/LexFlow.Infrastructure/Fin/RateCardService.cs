using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Fin;

/// <summary>Module 8: rate cards/entries + billing arrangements, and BR-7 rate resolution.</summary>
public sealed class RateCardService(LexFlowDbContext db) : IRateCardService
{
    public async Task<RateCardDto> CreateRateCardAsync(Guid tenantId, string name, Guid? branchId, bool isDefault, CancellationToken cancellationToken = default)
    {
        var card = new RateCard(tenantId, name, branchId, isDefault);
        await db.RateCards.AddAsync(card, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(card);
    }

    public async Task<IReadOnlyList<RateCardDto>> GetRateCardsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var cards = await db.RateCards.Where(c => c.TenantId == tenantId).OrderBy(c => c.Name).ToListAsync(cancellationToken);
        return cards.Select(ToDto).ToList();
    }

    public async Task<RateCardEntryDto> UpsertRateCardEntryAsync(Guid tenantId, Guid rateCardId, string? role, Guid? userId, decimal rate, string currency, DateOnly effectiveFrom, CancellationToken cancellationToken = default)
    {
        var existing = await db.RateCardEntries.SingleOrDefaultAsync(
            e => e.TenantId == tenantId && e.RateCardId == rateCardId && e.Role == role && e.UserId == userId && e.EffectiveFrom == effectiveFrom, cancellationToken);

        if (existing is not null)
        {
            existing.Update(rate, effectiveFrom);
            await db.SaveChangesAsync(cancellationToken);
            return ToDto(existing);
        }

        var entry = new RateCardEntry(tenantId, rateCardId, role, userId, rate, currency, effectiveFrom);
        await db.RateCardEntries.AddAsync(entry, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entry);
    }

    public async Task<BillingArrangementDto> SetBillingArrangementAsync(Guid tenantId, Guid matterId, SetBillingArrangementInput input, CancellationToken cancellationToken = default)
    {
        var existing = await db.BillingArrangements.SingleOrDefaultAsync(b => b.TenantId == tenantId && b.MatterId == matterId && b.IsActive, cancellationToken);
        existing?.Deactivate();

        var arrangement = new BillingArrangement(
            tenantId, matterId, input.ArrangementType, input.RateCardId, input.FixedAmount, input.MilestonesJson ?? "[]",
            input.RetainerAmount, input.RetainerPeriod, input.AutoInvoiceDay, input.ReplenishmentThreshold, input.ContingencyPct);
        await db.BillingArrangements.AddAsync(arrangement, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(arrangement);
    }

    public async Task<BillingArrangementDto?> GetBillingArrangementAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
    {
        var arrangement = await db.BillingArrangements.SingleOrDefaultAsync(b => b.TenantId == tenantId && b.MatterId == matterId && b.IsActive, cancellationToken);
        return arrangement is null ? null : ToDto(arrangement);
    }

    public async Task<decimal> ResolveRateAsync(Guid tenantId, Guid matterId, Guid userId, string? userRole, decimal? manualOverride, CancellationToken cancellationToken = default)
    {
        // BR-7: entry override -> matter-member override -> matter rate card -> firm default.
        if (manualOverride.HasValue)
        {
            return manualOverride.Value;
        }

        var arrangement = await db.BillingArrangements.SingleOrDefaultAsync(b => b.TenantId == tenantId && b.MatterId == matterId && b.IsActive, cancellationToken);

        if (arrangement?.RateCardId is { } matterCardId)
        {
            var matterRate = await ResolveFromCardAsync(tenantId, matterCardId, userId, userRole, cancellationToken);
            if (matterRate.HasValue)
            {
                return matterRate.Value;
            }
        }

        var defaultCard = await db.RateCards.SingleOrDefaultAsync(c => c.TenantId == tenantId && c.IsDefault && c.IsActive, cancellationToken);
        if (defaultCard is not null)
        {
            var firmRate = await ResolveFromCardAsync(tenantId, defaultCard.Id, userId, userRole, cancellationToken);
            if (firmRate.HasValue)
            {
                return firmRate.Value;
            }
        }

        throw new DomainRuleException("RATE_NOT_CONFIGURED", $"No rate could be resolved for user {userId} on matter {matterId} — configure a matter rate card or a firm default rate card (BR-7).");
    }

    private async Task<decimal?> ResolveFromCardAsync(Guid tenantId, Guid rateCardId, Guid userId, string? userRole, CancellationToken cancellationToken)
    {
        var entries = await db.RateCardEntries.Where(e => e.TenantId == tenantId && e.RateCardId == rateCardId).ToListAsync(cancellationToken);

        var userEntry = entries.Where(e => e.UserId == userId).OrderByDescending(e => e.EffectiveFrom).FirstOrDefault();
        if (userEntry is not null)
        {
            return userEntry.Rate;
        }

        if (userRole is not null)
        {
            var roleEntry = entries.Where(e => e.UserId is null && e.Role == userRole).OrderByDescending(e => e.EffectiveFrom).FirstOrDefault();
            if (roleEntry is not null)
            {
                return roleEntry.Rate;
            }
        }

        return null;
    }

    private static RateCardDto ToDto(RateCard c) => new(c.Id, c.Name, c.BranchId, c.IsDefault, c.IsActive);

    private static RateCardEntryDto ToDto(RateCardEntry e) => new(e.Id, e.RateCardId, e.Role, e.UserId, e.Rate, e.Currency, e.EffectiveFrom);

    private static BillingArrangementDto ToDto(BillingArrangement b) => new(b.Id, b.MatterId, b.ArrangementType, b.RateCardId, b.FixedAmount, b.MilestonesJson, b.RetainerAmount, b.RetainerPeriod, b.AutoInvoiceDay, b.ReplenishmentThreshold, b.ContingencyPct, b.IsActive);
}
