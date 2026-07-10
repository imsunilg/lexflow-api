using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Kb;

public sealed record CreateKbArticleDraftCommand(string Title, string? Body) : IRequest<KbArticleDto>;

public sealed class CreateKbArticleDraftCommandHandler(IKbArticleService service, ICurrentUserService currentUser) : IRequestHandler<CreateKbArticleDraftCommand, KbArticleDto>
{
    public Task<KbArticleDto> Handle(CreateKbArticleDraftCommand request, CancellationToken cancellationToken)
        => service.CreateDraftAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.Title, request.Body, cancellationToken);
}

public sealed record UpdateKbArticleDraftCommand(Guid Id, string Title, string? Body) : IRequest<KbArticleDto>;

public sealed class UpdateKbArticleDraftCommandHandler(IKbArticleService service, ICurrentUserService currentUser) : IRequestHandler<UpdateKbArticleDraftCommand, KbArticleDto>
{
    public Task<KbArticleDto> Handle(UpdateKbArticleDraftCommand request, CancellationToken cancellationToken)
        => service.UpdateDraftAsync(currentUser.TenantId!.Value, request.Id, request.Title, request.Body, cancellationToken);
}

public sealed record SubmitKbArticleForReviewCommand(Guid Id) : IRequest<KbArticleDto>;

public sealed class SubmitKbArticleForReviewCommandHandler(IKbArticleService service, ICurrentUserService currentUser) : IRequestHandler<SubmitKbArticleForReviewCommand, KbArticleDto>
{
    public Task<KbArticleDto> Handle(SubmitKbArticleForReviewCommand request, CancellationToken cancellationToken)
        => service.SubmitForReviewAsync(currentUser.TenantId!.Value, request.Id, cancellationToken);
}

public sealed record AssignKbArticleReviewerCommand(Guid Id, Guid ReviewerId) : IRequest<KbArticleDto>;

public sealed class AssignKbArticleReviewerCommandHandler(IKbArticleService service, ICurrentUserService currentUser) : IRequestHandler<AssignKbArticleReviewerCommand, KbArticleDto>
{
    public Task<KbArticleDto> Handle(AssignKbArticleReviewerCommand request, CancellationToken cancellationToken)
        => service.AssignReviewerAsync(currentUser.TenantId!.Value, request.Id, request.ReviewerId, cancellationToken);
}

public sealed record SendKbArticleBackToDraftCommand(Guid Id) : IRequest<KbArticleDto>;

public sealed class SendKbArticleBackToDraftCommandHandler(IKbArticleService service, ICurrentUserService currentUser) : IRequestHandler<SendKbArticleBackToDraftCommand, KbArticleDto>
{
    public Task<KbArticleDto> Handle(SendKbArticleBackToDraftCommand request, CancellationToken cancellationToken)
        => service.SendBackToDraftAsync(currentUser.TenantId!.Value, request.Id, cancellationToken);
}

public sealed record PublishKbArticleCommand(Guid Id) : IRequest<KbArticleDto>;

public sealed class PublishKbArticleCommandHandler(IKbArticleService service, ICurrentUserService currentUser) : IRequestHandler<PublishKbArticleCommand, KbArticleDto>
{
    public Task<KbArticleDto> Handle(PublishKbArticleCommand request, CancellationToken cancellationToken)
        => service.PublishAsync(currentUser.TenantId!.Value, request.Id, cancellationToken);
}

/// <summary>
/// PRD §17 lists submit|approve|publish as three distinct actions, but kb.kb_articles' own status
/// CHECK constraint only has Draft/InReview/Published — there is no persisted "Approved" state
/// between review and publish in this schema. "Approve" is therefore the reviewer's one-shot
/// action: assign themselves as reviewer (reviewer≠author enforced by AssignReviewerAsync) and
/// publish in the same call. A reviewer who was assigned earlier (via AssignKbArticleReviewerCommand)
/// can still use the plain PublishKbArticleCommand directly.
/// </summary>
public sealed record ApproveKbArticleCommand(Guid Id, Guid ReviewerId) : IRequest<KbArticleDto>;

public sealed class ApproveKbArticleCommandHandler(IKbArticleService service, ICurrentUserService currentUser) : IRequestHandler<ApproveKbArticleCommand, KbArticleDto>
{
    public async Task<KbArticleDto> Handle(ApproveKbArticleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId!.Value;
        await service.AssignReviewerAsync(tenantId, request.Id, request.ReviewerId, cancellationToken);
        return await service.PublishAsync(tenantId, request.Id, cancellationToken);
    }
}
