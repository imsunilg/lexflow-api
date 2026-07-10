namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 15 §8 Taxes CRUD (PRD §17: "CRUD number-series, tax-rates, templates").</summary>
public interface ITaxRateService
{
    Task<IReadOnlyList<TaxRateDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<TaxRateDto> CreateAsync(Guid tenantId, string countryCode, string taxType, string componentsJson, Guid? branchId, CancellationToken cancellationToken = default);

    Task<TaxRateDto> UpdateAsync(Guid tenantId, Guid id, string countryCode, string taxType, string componentsJson, bool isActive, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
}

public sealed record TaxRateDto(Guid Id, string CountryCode, string TaxType, string ComponentsJson, bool IsActive, Guid? BranchId);
