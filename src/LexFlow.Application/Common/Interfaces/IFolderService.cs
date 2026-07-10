namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 7 folder tree — PRD §17 API list. Depth ≤ 10 enforced here (Validation Rules).</summary>
public interface IFolderService
{
    Task<FolderDto> CreateAsync(Guid tenantId, Guid? actorId, string name, Guid? parentId, Guid? matterId, CancellationToken cancellationToken = default);

    Task<FolderDto?> GetByIdAsync(Guid tenantId, Guid folderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FolderDto>> GetAllAsync(Guid tenantId, Guid? parentId, Guid? matterId, CancellationToken cancellationToken = default);

    Task<FolderDto> UpdateAsync(Guid tenantId, Guid folderId, string name, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid tenantId, Guid folderId, CancellationToken cancellationToken = default);

    Task<FolderDto> MoveAsync(Guid tenantId, Guid folderId, Guid? newParentId, CancellationToken cancellationToken = default);
}

public sealed record FolderDto(Guid Id, string Name, Guid? ParentId, Guid? MatterId, string? Path);
