using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Kb;

/// <summary>Module 12: taxonomy tags, curated collections, and personal bookmarks — each polymorphic over Act/ActSection/Judgment/Article/Template.</summary>
public sealed class KbTaxonomyService(LexFlowDbContext db) : IKbTaxonomyService
{
    public async Task<KbTagDto> GetOrCreateTagAsync(Guid tenantId, string name, CancellationToken cancellationToken = default)
    {
        var tag = await db.KbTags.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Name == name, cancellationToken);
        if (tag is not null)
        {
            return ToDto(tag);
        }

        tag = new KbTag(tenantId, name);
        await db.KbTags.AddAsync(tag, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(tag);
    }

    public async Task<IReadOnlyList<KbTagDto>> GetTagsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tags = await db.KbTags.Where(t => t.TenantId == tenantId).OrderBy(t => t.Name).ToListAsync(cancellationToken);
        return tags.Select(ToDto).ToList();
    }

    public async Task AttachTagAsync(Guid tenantId, string kbRefKind, Guid kbRefId, string tagName, CancellationToken cancellationToken = default)
    {
        var tag = await GetOrCreateTagAsync(tenantId, tagName, cancellationToken);
        var alreadyAttached = await db.KbItemTags.AnyAsync(t => t.TenantId == tenantId && t.TagId == tag.Id && t.KbRefKind == kbRefKind && t.KbRefId == kbRefId, cancellationToken);
        if (alreadyAttached)
        {
            return;
        }

        await db.KbItemTags.AddAsync(new KbItemTag(tenantId, tag.Id, kbRefKind, kbRefId), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DetachTagAsync(Guid tenantId, string kbRefKind, Guid kbRefId, string tagName, CancellationToken cancellationToken = default)
    {
        var tag = await db.KbTags.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Name == tagName, cancellationToken);
        if (tag is null)
        {
            return;
        }

        var link = await db.KbItemTags.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.TagId == tag.Id && t.KbRefKind == kbRefKind && t.KbRefId == kbRefId, cancellationToken);
        if (link is null)
        {
            return;
        }

        db.KbItemTags.Remove(link);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetTagNamesAsync(Guid tenantId, string kbRefKind, Guid kbRefId, CancellationToken cancellationToken = default)
        => await db.KbItemTags.Where(t => t.TenantId == tenantId && t.KbRefKind == kbRefKind && t.KbRefId == kbRefId)
            .Join(db.KbTags, it => it.TagId, t => t.Id, (it, t) => t.Name)
            .ToListAsync(cancellationToken);

    public async Task<KbCollectionDto> CreateCollectionAsync(Guid tenantId, string name, string? description, CancellationToken cancellationToken = default)
    {
        var collection = new KbCollection(tenantId, name, description);
        await db.KbCollections.AddAsync(collection, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(collection);
    }

    public async Task<IReadOnlyList<KbCollectionDto>> GetCollectionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var collections = await db.KbCollections.Where(c => c.TenantId == tenantId).OrderBy(c => c.Name).ToListAsync(cancellationToken);
        return collections.Select(ToDto).ToList();
    }

    public async Task AddToCollectionAsync(Guid tenantId, Guid collectionId, string kbRefKind, Guid kbRefId, CancellationToken cancellationToken = default)
    {
        var exists = await db.KbCollections.AnyAsync(c => c.TenantId == tenantId && c.Id == collectionId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException(nameof(KbCollection), collectionId);
        }

        var alreadyAdded = await db.KbCollectionItems.AnyAsync(i => i.TenantId == tenantId && i.CollectionId == collectionId && i.KbRefKind == kbRefKind && i.KbRefId == kbRefId, cancellationToken);
        if (alreadyAdded)
        {
            return;
        }

        var nextOrder = await db.KbCollectionItems.Where(i => i.TenantId == tenantId && i.CollectionId == collectionId).CountAsync(cancellationToken);
        await db.KbCollectionItems.AddAsync(new KbCollectionItem(tenantId, collectionId, kbRefKind, kbRefId, nextOrder), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<KbCollectionItemDto>> GetCollectionItemsAsync(Guid tenantId, Guid collectionId, CancellationToken cancellationToken = default)
    {
        var items = await db.KbCollectionItems.Where(i => i.TenantId == tenantId && i.CollectionId == collectionId).OrderBy(i => i.SortOrder).ToListAsync(cancellationToken);
        return items.Select(i => new KbCollectionItemDto(i.Id, i.KbRefKind, i.KbRefId, i.SortOrder)).ToList();
    }

    public async Task<KbBookmarkDto> AddBookmarkAsync(Guid tenantId, Guid userId, string kbRefKind, Guid kbRefId, CancellationToken cancellationToken = default)
    {
        var existing = await db.KbBookmarks.SingleOrDefaultAsync(b => b.TenantId == tenantId && b.UserId == userId && b.KbRefKind == kbRefKind && b.KbRefId == kbRefId, cancellationToken);
        if (existing is not null)
        {
            return ToDto(existing);
        }

        var bookmark = new KbBookmark(tenantId, userId, kbRefKind, kbRefId);
        await db.KbBookmarks.AddAsync(bookmark, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(bookmark);
    }

    public async Task RemoveBookmarkAsync(Guid tenantId, Guid userId, string kbRefKind, Guid kbRefId, CancellationToken cancellationToken = default)
    {
        var bookmark = await db.KbBookmarks.SingleOrDefaultAsync(b => b.TenantId == tenantId && b.UserId == userId && b.KbRefKind == kbRefKind && b.KbRefId == kbRefId, cancellationToken);
        if (bookmark is null)
        {
            return;
        }

        db.KbBookmarks.Remove(bookmark);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<KbBookmarkDto>> GetBookmarksAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var bookmarks = await db.KbBookmarks.Where(b => b.TenantId == tenantId && b.UserId == userId).ToListAsync(cancellationToken);
        return bookmarks.Select(ToDto).ToList();
    }

    private static KbTagDto ToDto(KbTag t) => new(t.Id, t.Name);

    private static KbCollectionDto ToDto(KbCollection c) => new(c.Id, c.Name, c.Description);

    private static KbBookmarkDto ToDto(KbBookmark b) => new(b.Id, b.KbRefKind, b.KbRefId);
}
