namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 14 Branches CRUD (PRD §17: CRUD /api/v1/branches). Deletion blocked while users/matters attached (PRD Module 14 Validation).</summary>
public interface IBranchService
{
    Task<IReadOnlyList<BranchDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<BranchDto?> GetByIdAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default);

    Task<BranchDto> CreateAsync(Guid tenantId, CreateBranchInput input, CancellationToken cancellationToken = default);

    Task<BranchDto> UpdateAsync(Guid tenantId, Guid branchId, CreateBranchInput input, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default);
}

public sealed record CreateBranchInput(string Name, string Code, string AddressJson, string Tz, string? Gstin, string? SeriesPrefix);

public sealed record BranchDto(Guid Id, string Name, string Code, string AddressJson, string Tz, string? Gstin, string? SeriesPrefix);
