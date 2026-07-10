namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 12: tags (taxonomy), collections (curated sets), and personal bookmarks — each polymorphic over Act/ActSection/Judgment/Article/Template via (kbRefKind, kbRefId).</summary>
public interface IKbTaxonomyService
{
    Task<KbTagDto> GetOrCreateTagAsync(Guid tenantId, string name, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KbTagDto>> GetTagsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task AttachTagAsync(Guid tenantId, string kbRefKind, Guid kbRefId, string tagName, CancellationToken cancellationToken = default);

    Task DetachTagAsync(Guid tenantId, string kbRefKind, Guid kbRefId, string tagName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetTagNamesAsync(Guid tenantId, string kbRefKind, Guid kbRefId, CancellationToken cancellationToken = default);

    Task<KbCollectionDto> CreateCollectionAsync(Guid tenantId, string name, string? description, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KbCollectionDto>> GetCollectionsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task AddToCollectionAsync(Guid tenantId, Guid collectionId, string kbRefKind, Guid kbRefId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KbCollectionItemDto>> GetCollectionItemsAsync(Guid tenantId, Guid collectionId, CancellationToken cancellationToken = default);

    Task<KbBookmarkDto> AddBookmarkAsync(Guid tenantId, Guid userId, string kbRefKind, Guid kbRefId, CancellationToken cancellationToken = default);

    Task RemoveBookmarkAsync(Guid tenantId, Guid userId, string kbRefKind, Guid kbRefId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KbBookmarkDto>> GetBookmarksAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
}

public sealed record KbTagDto(Guid Id, string Name);

public sealed record KbCollectionDto(Guid Id, string Name, string? Description);

public sealed record KbCollectionItemDto(Guid Id, string KbRefKind, Guid KbRefId, int SortOrder);

public sealed record KbBookmarkDto(Guid Id, string KbRefKind, Guid KbRefId);
