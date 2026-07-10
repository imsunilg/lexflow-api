using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Kb;

public sealed record GetKbTagsQuery : IRequest<IReadOnlyList<KbTagDto>>;

public sealed class GetKbTagsQueryHandler(IKbTaxonomyService service, ICurrentUserService currentUser) : IRequestHandler<GetKbTagsQuery, IReadOnlyList<KbTagDto>>
{
    public Task<IReadOnlyList<KbTagDto>> Handle(GetKbTagsQuery request, CancellationToken cancellationToken)
        => service.GetTagsAsync(currentUser.TenantId!.Value, cancellationToken);
}

public sealed record GetKbCollectionsQuery : IRequest<IReadOnlyList<KbCollectionDto>>;

public sealed class GetKbCollectionsQueryHandler(IKbTaxonomyService service, ICurrentUserService currentUser) : IRequestHandler<GetKbCollectionsQuery, IReadOnlyList<KbCollectionDto>>
{
    public Task<IReadOnlyList<KbCollectionDto>> Handle(GetKbCollectionsQuery request, CancellationToken cancellationToken)
        => service.GetCollectionsAsync(currentUser.TenantId!.Value, cancellationToken);
}

public sealed record GetKbCollectionItemsQuery(Guid CollectionId) : IRequest<IReadOnlyList<KbCollectionItemDto>>;

public sealed class GetKbCollectionItemsQueryHandler(IKbTaxonomyService service, ICurrentUserService currentUser) : IRequestHandler<GetKbCollectionItemsQuery, IReadOnlyList<KbCollectionItemDto>>
{
    public Task<IReadOnlyList<KbCollectionItemDto>> Handle(GetKbCollectionItemsQuery request, CancellationToken cancellationToken)
        => service.GetCollectionItemsAsync(currentUser.TenantId!.Value, request.CollectionId, cancellationToken);
}

public sealed record GetKbBookmarksQuery : IRequest<IReadOnlyList<KbBookmarkDto>>;

public sealed class GetKbBookmarksQueryHandler(IKbTaxonomyService service, ICurrentUserService currentUser) : IRequestHandler<GetKbBookmarksQuery, IReadOnlyList<KbBookmarkDto>>
{
    public Task<IReadOnlyList<KbBookmarkDto>> Handle(GetKbBookmarksQuery request, CancellationToken cancellationToken)
        => service.GetBookmarksAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, cancellationToken);
}
