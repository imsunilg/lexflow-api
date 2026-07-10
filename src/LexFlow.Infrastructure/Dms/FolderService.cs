using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Dms;

/// <summary>Module 7 folder tree. Depth ≤ 10 (Validation Rules).</summary>
public sealed class FolderService(LexFlowDbContext db) : IFolderService
{
    private const int MaxDepth = 10;

    public async Task<FolderDto> CreateAsync(Guid tenantId, Guid? actorId, string name, Guid? parentId, Guid? matterId, CancellationToken cancellationToken = default)
    {
        string? path = null;

        if (parentId.HasValue)
        {
            var parent = await db.Folders.SingleOrDefaultAsync(f => f.TenantId == tenantId && f.Id == parentId, cancellationToken)
                ?? throw new NotFoundException(nameof(Folder), parentId.Value);

            var depth = (parent.Path?.Split('.').Length ?? 0) + 1;
            if (depth > MaxDepth)
            {
                throw new ConflictException($"Folder depth cannot exceed {MaxDepth}.", "FOLDER_DEPTH_EXCEEDED");
            }

            path = string.IsNullOrEmpty(parent.Path) ? SegmentOf(parent.Id) : $"{parent.Path}.{SegmentOf(parent.Id)}";
        }

        var duplicate = await db.Folders.AnyAsync(f => f.TenantId == tenantId && f.ParentId == parentId && f.Name == name, cancellationToken);
        if (duplicate)
        {
            throw new ConflictException($"A folder named '{name}' already exists at this level.", "FOLDER_NAME_EXISTS");
        }

        var folder = new Folder(tenantId, name, parentId, matterId, path);
        await db.Folders.AddAsync(folder, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(folder);
    }

    public async Task<FolderDto?> GetByIdAsync(Guid tenantId, Guid folderId, CancellationToken cancellationToken = default)
    {
        var folder = await db.Folders.SingleOrDefaultAsync(f => f.TenantId == tenantId && f.Id == folderId, cancellationToken);
        return folder is null ? null : ToDto(folder);
    }

    public async Task<IReadOnlyList<FolderDto>> GetAllAsync(Guid tenantId, Guid? parentId, Guid? matterId, CancellationToken cancellationToken = default)
    {
        var query = db.Folders.Where(f => f.TenantId == tenantId).AsQueryable();
        if (parentId.HasValue)
        {
            query = query.Where(f => f.ParentId == parentId);
        }

        if (matterId.HasValue)
        {
            query = query.Where(f => f.MatterId == matterId);
        }

        var folders = await query.OrderBy(f => f.Name).ToListAsync(cancellationToken);
        return folders.Select(ToDto).ToList();
    }

    public async Task<FolderDto> UpdateAsync(Guid tenantId, Guid folderId, string name, CancellationToken cancellationToken = default)
    {
        var folder = await GetOrThrowAsync(tenantId, folderId, cancellationToken);
        folder.Rename(name);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(folder);
    }

    public async Task DeleteAsync(Guid tenantId, Guid folderId, CancellationToken cancellationToken = default)
    {
        var folder = await GetOrThrowAsync(tenantId, folderId, cancellationToken);

        var hasChildren = await db.Folders.AnyAsync(f => f.TenantId == tenantId && f.ParentId == folderId, cancellationToken);
        var hasDocuments = await db.Documents.AnyAsync(d => d.TenantId == tenantId && d.FolderId == folderId, cancellationToken);
        if (hasChildren || hasDocuments)
        {
            throw new ConflictException("Cannot delete a folder that still contains subfolders or documents.", "FOLDER_NOT_EMPTY");
        }

        db.Folders.Remove(folder);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<FolderDto> MoveAsync(Guid tenantId, Guid folderId, Guid? newParentId, CancellationToken cancellationToken = default)
    {
        var folder = await GetOrThrowAsync(tenantId, folderId, cancellationToken);

        if (newParentId == folderId)
        {
            throw new ConflictException("A folder cannot be moved into itself.", "FOLDER_SELF_MOVE");
        }

        string? newPath = null;
        if (newParentId.HasValue)
        {
            var newParent = await db.Folders.SingleOrDefaultAsync(f => f.TenantId == tenantId && f.Id == newParentId, cancellationToken)
                ?? throw new NotFoundException(nameof(Folder), newParentId.Value);

            // Guard against moving a folder into its own descendant (would create a cycle).
            if (newParent.Path?.Split('.').Contains(SegmentOf(folderId)) == true)
            {
                throw new ConflictException("Cannot move a folder into one of its own descendants.", "FOLDER_CYCLE");
            }

            var depth = (newParent.Path?.Split('.').Length ?? 0) + 1;
            if (depth > MaxDepth)
            {
                throw new ConflictException($"Folder depth cannot exceed {MaxDepth}.", "FOLDER_DEPTH_EXCEEDED");
            }

            newPath = string.IsNullOrEmpty(newParent.Path) ? SegmentOf(newParent.Id) : $"{newParent.Path}.{SegmentOf(newParent.Id)}";
        }

        folder.MoveTo(newParentId, newPath);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(folder);
    }

    private async Task<Folder> GetOrThrowAsync(Guid tenantId, Guid folderId, CancellationToken cancellationToken)
        => await db.Folders.SingleOrDefaultAsync(f => f.TenantId == tenantId && f.Id == folderId, cancellationToken)
           ?? throw new NotFoundException(nameof(Folder), folderId);

    private static string SegmentOf(Guid id) => $"f{id:N}";

    private static FolderDto ToDto(Folder f) => new(f.Id, f.Name, f.ParentId, f.MatterId, f.Path);
}
