using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Kb;

/// <summary>AC-KB5: unreviewed article never visible to non-authors — callerCanReview is derived from the caller's kb.review.all permission, resolved here so every handler applies the same rule.</summary>
public sealed record GetKbArticleQuery(Guid Id) : IRequest<KbArticleDto?>;

public sealed class GetKbArticleQueryHandler(IKbArticleService service, ICurrentUserService currentUser) : IRequestHandler<GetKbArticleQuery, KbArticleDto?>
{
    public Task<KbArticleDto?> Handle(GetKbArticleQuery request, CancellationToken cancellationToken)
        => service.GetAsync(currentUser.TenantId!.Value, request.Id, currentUser.UserId!.Value, currentUser.Permissions.Contains("kb.review.all"), cancellationToken);
}

public sealed record GetKbArticlesQuery : IRequest<IReadOnlyList<KbArticleDto>>;

public sealed class GetKbArticlesQueryHandler(IKbArticleService service, ICurrentUserService currentUser) : IRequestHandler<GetKbArticlesQuery, IReadOnlyList<KbArticleDto>>
{
    public Task<IReadOnlyList<KbArticleDto>> Handle(GetKbArticlesQuery request, CancellationToken cancellationToken)
        => service.GetVisibleAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, currentUser.Permissions.Contains("kb.review.all"), cancellationToken);
}

public sealed record GetKbArticleVersionsQuery(Guid Id) : IRequest<IReadOnlyList<KbArticleVersionDto>>;

public sealed class GetKbArticleVersionsQueryHandler(IKbArticleService service, ICurrentUserService currentUser) : IRequestHandler<GetKbArticleVersionsQuery, IReadOnlyList<KbArticleVersionDto>>
{
    public Task<IReadOnlyList<KbArticleVersionDto>> Handle(GetKbArticleVersionsQuery request, CancellationToken cancellationToken)
        => service.GetVersionsAsync(currentUser.TenantId!.Value, request.Id, cancellationToken);
}
