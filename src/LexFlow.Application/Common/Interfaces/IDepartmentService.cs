namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 14 Departments CRUD (PRD §17: CRUD /api/v1/departments).</summary>
public interface IDepartmentService
{
    Task<IReadOnlyList<DepartmentDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<DepartmentDto?> GetByIdAsync(Guid tenantId, Guid departmentId, CancellationToken cancellationToken = default);

    Task<DepartmentDto> CreateAsync(Guid tenantId, string name, string? code, Guid? headUserId, CancellationToken cancellationToken = default);

    Task<DepartmentDto> UpdateAsync(Guid tenantId, Guid departmentId, string name, string? code, Guid? headUserId, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid tenantId, Guid departmentId, CancellationToken cancellationToken = default);
}

public sealed record DepartmentDto(Guid Id, string Name, string? Code, Guid? HeadUserId);
