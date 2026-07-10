using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Management;

/// <summary>Module 14 Departments CRUD (PRD §17).</summary>
public sealed class DepartmentService(LexFlowDbContext db) : IDepartmentService
{
    public async Task<IReadOnlyList<DepartmentDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var departments = await db.Departments.Where(d => d.TenantId == tenantId).OrderBy(d => d.Name).ToListAsync(cancellationToken);
        return departments.Select(ToDto).ToList();
    }

    public async Task<DepartmentDto?> GetByIdAsync(Guid tenantId, Guid departmentId, CancellationToken cancellationToken = default)
    {
        var department = await db.Departments.SingleOrDefaultAsync(d => d.TenantId == tenantId && d.Id == departmentId, cancellationToken);
        return department is null ? null : ToDto(department);
    }

    public async Task<DepartmentDto> CreateAsync(Guid tenantId, string name, string? code, Guid? headUserId, CancellationToken cancellationToken = default)
    {
        var department = new Department(tenantId, name, code, headUserId);
        await db.Departments.AddAsync(department, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(department);
    }

    public async Task<DepartmentDto> UpdateAsync(Guid tenantId, Guid departmentId, string name, string? code, Guid? headUserId, CancellationToken cancellationToken = default)
    {
        var department = await db.Departments.SingleOrDefaultAsync(d => d.TenantId == tenantId && d.Id == departmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Department), departmentId);

        department.Update(name, code, headUserId);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(department);
    }

    public async Task DeleteAsync(Guid tenantId, Guid departmentId, CancellationToken cancellationToken = default)
    {
        var department = await db.Departments.SingleOrDefaultAsync(d => d.TenantId == tenantId && d.Id == departmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Department), departmentId);

        var usersAttached = await db.Users.AnyAsync(u => u.TenantId == tenantId && u.DepartmentId == departmentId, cancellationToken);
        if (usersAttached)
        {
            throw new ConflictException("Cannot delete a department with users attached.", "DEPARTMENT_IN_USE");
        }

        db.Departments.Remove(department);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static DepartmentDto ToDto(Department department) => new(department.Id, department.Name, department.Code, department.HeadUserId);
}
