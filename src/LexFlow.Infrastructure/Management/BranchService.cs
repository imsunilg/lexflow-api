using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Management;

/// <summary>Module 14 Branches CRUD (PRD §17). Deletion blocked while users are attached (PRD Module 14 Validation).</summary>
public sealed class BranchService(LexFlowDbContext db) : IBranchService
{
    public async Task<IReadOnlyList<BranchDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var branches = await db.Branches.Where(b => b.TenantId == tenantId).OrderBy(b => b.Name).ToListAsync(cancellationToken);
        return branches.Select(ToDto).ToList();
    }

    public async Task<BranchDto?> GetByIdAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default)
    {
        var branch = await db.Branches.SingleOrDefaultAsync(b => b.TenantId == tenantId && b.Id == branchId, cancellationToken);
        return branch is null ? null : ToDto(branch);
    }

    public async Task<BranchDto> CreateAsync(Guid tenantId, CreateBranchInput input, CancellationToken cancellationToken = default)
    {
        var branch = new Branch(tenantId, input.Name, input.Code, input.AddressJson, input.Tz);
        branch.Update(input.Name, input.Code, input.AddressJson, input.Tz, input.Gstin, input.SeriesPrefix);
        await db.Branches.AddAsync(branch, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(branch);
    }

    public async Task<BranchDto> UpdateAsync(Guid tenantId, Guid branchId, CreateBranchInput input, CancellationToken cancellationToken = default)
    {
        var branch = await db.Branches.SingleOrDefaultAsync(b => b.TenantId == tenantId && b.Id == branchId, cancellationToken)
            ?? throw new NotFoundException(nameof(Branch), branchId);

        branch.Update(input.Name, input.Code, input.AddressJson, input.Tz, input.Gstin, input.SeriesPrefix);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(branch);
    }

    public async Task DeleteAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default)
    {
        var branch = await db.Branches.SingleOrDefaultAsync(b => b.TenantId == tenantId && b.Id == branchId, cancellationToken)
            ?? throw new NotFoundException(nameof(Branch), branchId);

        var usersAttached = await db.Users.AnyAsync(u => u.TenantId == tenantId && u.BranchId == branchId, cancellationToken);
        if (usersAttached)
        {
            throw new ConflictException("Cannot delete a branch with users attached.", "BRANCH_IN_USE");
        }

        db.Branches.Remove(branch);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static BranchDto ToDto(Branch branch) => new(branch.Id, branch.Name, branch.Code, branch.Address, branch.Tz, branch.Gstin, branch.SeriesPrefix);
}
