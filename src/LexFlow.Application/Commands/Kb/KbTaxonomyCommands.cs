using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Kb;

public sealed record AttachKbTagCommand(string KbRefKind, Guid KbRefId, string TagName) : IRequest;

public sealed class AttachKbTagCommandHandler(IKbTaxonomyService service, ICurrentUserService currentUser) : IRequestHandler<AttachKbTagCommand>
{
    public async Task Handle(AttachKbTagCommand request, CancellationToken cancellationToken)
        => await service.AttachTagAsync(currentUser.TenantId!.Value, request.KbRefKind, request.KbRefId, request.TagName, cancellationToken);
}

public sealed record DetachKbTagCommand(string KbRefKind, Guid KbRefId, string TagName) : IRequest;

public sealed class DetachKbTagCommandHandler(IKbTaxonomyService service, ICurrentUserService currentUser) : IRequestHandler<DetachKbTagCommand>
{
    public async Task Handle(DetachKbTagCommand request, CancellationToken cancellationToken)
        => await service.DetachTagAsync(currentUser.TenantId!.Value, request.KbRefKind, request.KbRefId, request.TagName, cancellationToken);
}

public sealed record CreateKbCollectionCommand(string Name, string? Description) : IRequest<KbCollectionDto>;

public sealed class CreateKbCollectionCommandHandler(IKbTaxonomyService service, ICurrentUserService currentUser) : IRequestHandler<CreateKbCollectionCommand, KbCollectionDto>
{
    public Task<KbCollectionDto> Handle(CreateKbCollectionCommand request, CancellationToken cancellationToken)
        => service.CreateCollectionAsync(currentUser.TenantId!.Value, request.Name, request.Description, cancellationToken);
}

public sealed record AddToKbCollectionCommand(Guid CollectionId, string KbRefKind, Guid KbRefId) : IRequest;

public sealed class AddToKbCollectionCommandHandler(IKbTaxonomyService service, ICurrentUserService currentUser) : IRequestHandler<AddToKbCollectionCommand>
{
    public async Task Handle(AddToKbCollectionCommand request, CancellationToken cancellationToken)
        => await service.AddToCollectionAsync(currentUser.TenantId!.Value, request.CollectionId, request.KbRefKind, request.KbRefId, cancellationToken);
}

public sealed record AddKbBookmarkCommand(string KbRefKind, Guid KbRefId) : IRequest<KbBookmarkDto>;

public sealed class AddKbBookmarkCommandHandler(IKbTaxonomyService service, ICurrentUserService currentUser) : IRequestHandler<AddKbBookmarkCommand, KbBookmarkDto>
{
    public Task<KbBookmarkDto> Handle(AddKbBookmarkCommand request, CancellationToken cancellationToken)
        => service.AddBookmarkAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.KbRefKind, request.KbRefId, cancellationToken);
}

public sealed record RemoveKbBookmarkCommand(string KbRefKind, Guid KbRefId) : IRequest;

public sealed class RemoveKbBookmarkCommandHandler(IKbTaxonomyService service, ICurrentUserService currentUser) : IRequestHandler<RemoveKbBookmarkCommand>
{
    public async Task Handle(RemoveKbBookmarkCommand request, CancellationToken cancellationToken)
        => await service.RemoveBookmarkAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.KbRefKind, request.KbRefId, cancellationToken);
}
