namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 8: rate cards + entries (BR-7 rate resolution source data) and billing arrangements (fee setup per matter).</summary>
public interface IRateCardService
{
    Task<RateCardDto> CreateRateCardAsync(Guid tenantId, string name, Guid? branchId, bool isDefault, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RateCardDto>> GetRateCardsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<RateCardEntryDto> UpsertRateCardEntryAsync(Guid tenantId, Guid rateCardId, string? role, Guid? userId, decimal rate, string currency, DateOnly effectiveFrom, CancellationToken cancellationToken = default);

    Task<BillingArrangementDto> SetBillingArrangementAsync(Guid tenantId, Guid matterId, SetBillingArrangementInput input, CancellationToken cancellationToken = default);

    Task<BillingArrangementDto?> GetBillingArrangementAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default);

    /// <summary>BR-7: entry override -&gt; matter-member override -&gt; matter rate card -&gt; firm default. Throws DomainRuleException("RATE_NOT_CONFIGURED", ...) if none resolve.</summary>
    Task<decimal> ResolveRateAsync(Guid tenantId, Guid matterId, Guid userId, string? userRole, decimal? manualOverride, CancellationToken cancellationToken = default);
}

public sealed record RateCardDto(Guid Id, string Name, Guid? BranchId, bool IsDefault, bool IsActive);

public sealed record RateCardEntryDto(Guid Id, Guid RateCardId, string? Role, Guid? UserId, decimal Rate, string Currency, DateOnly EffectiveFrom);

public sealed record SetBillingArrangementInput(
    string ArrangementType, Guid? RateCardId, decimal? FixedAmount, string? MilestonesJson,
    decimal? RetainerAmount, string? RetainerPeriod, int? AutoInvoiceDay, decimal? ReplenishmentThreshold, decimal? ContingencyPct);

public sealed record BillingArrangementDto(
    Guid Id, Guid MatterId, string ArrangementType, Guid? RateCardId, decimal? FixedAmount, string MilestonesJson,
    decimal? RetainerAmount, string? RetainerPeriod, int? AutoInvoiceDay, decimal? ReplenishmentThreshold, decimal? ContingencyPct, bool IsActive);
