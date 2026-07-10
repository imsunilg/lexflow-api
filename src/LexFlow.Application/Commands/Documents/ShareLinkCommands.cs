using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Documents;

/// <summary>POST /api/v1/documents/{id}/share-links.</summary>
public sealed record CreateShareLinkCommand(Guid DocumentId, DateTimeOffset ExpiresAt, string? Password, int? MaxDownloads, bool Watermark) : IRequest<CreateShareLinkResult>;

public sealed class CreateShareLinkCommandHandler(IDocumentShareLinkService shareLinkService, ICurrentUserService currentUser) : IRequestHandler<CreateShareLinkCommand, CreateShareLinkResult>
{
    public Task<CreateShareLinkResult> Handle(CreateShareLinkCommand request, CancellationToken cancellationToken)
        => shareLinkService.CreateAsync(currentUser.TenantId!.Value, currentUser.UserId, request.DocumentId, request.ExpiresAt, request.Password, request.MaxDownloads, request.Watermark, cancellationToken);
}

/// <summary>DELETE /api/v1/share-links/{id}.</summary>
public sealed record RevokeShareLinkCommand(Guid ShareLinkId) : IRequest;

public sealed class RevokeShareLinkCommandHandler(IDocumentShareLinkService shareLinkService, ICurrentUserService currentUser) : IRequestHandler<RevokeShareLinkCommand>
{
    public async Task Handle(RevokeShareLinkCommand request, CancellationToken cancellationToken)
        => await shareLinkService.RevokeAsync(currentUser.TenantId!.Value, request.ShareLinkId, cancellationToken);
}

/// <summary>GET /api/v1/public/shared/{token} (public, guarded). AC-DOC5.</summary>
public sealed record AccessShareLinkCommand(string Token, string? Password, string? Ip, string? UserAgent) : IRequest<ShareLinkAccessResult>;

public sealed class AccessShareLinkCommandHandler(IDocumentShareLinkService shareLinkService) : IRequestHandler<AccessShareLinkCommand, ShareLinkAccessResult>
{
    public Task<ShareLinkAccessResult> Handle(AccessShareLinkCommand request, CancellationToken cancellationToken)
        => shareLinkService.AccessAsync(request.Token, request.Password, request.Ip, request.UserAgent, cancellationToken);
}
