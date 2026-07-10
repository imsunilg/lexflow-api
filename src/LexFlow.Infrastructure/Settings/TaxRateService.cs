using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Settings;

/// <summary>Module 15 §8 Taxes CRUD.</summary>
public sealed class TaxRateService(LexFlowDbContext db) : ITaxRateService
{
    public async Task<IReadOnlyList<TaxRateDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var configs = await db.TaxConfigs.Where(t => t.TenantId == tenantId).ToListAsync(cancellationToken);
        return configs.Select(ToDto).ToList();
    }

    public async Task<TaxRateDto> CreateAsync(Guid tenantId, string countryCode, string taxType, string componentsJson, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var config = new TaxConfig(tenantId, countryCode, taxType, componentsJson, branchId);
        await db.TaxConfigs.AddAsync(config, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(config);
    }

    public async Task<TaxRateDto> UpdateAsync(Guid tenantId, Guid id, string countryCode, string taxType, string componentsJson, bool isActive, CancellationToken cancellationToken = default)
    {
        var config = await db.TaxConfigs.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(TaxConfig), id);

        config.Update(countryCode, taxType, componentsJson, isActive);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(config);
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        var config = await db.TaxConfigs.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(TaxConfig), id);

        db.TaxConfigs.Remove(config);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static TaxRateDto ToDto(TaxConfig config) => new(config.Id, config.CountryCode, config.TaxType, config.ComponentsJson, config.IsActive, config.BranchId);
}
