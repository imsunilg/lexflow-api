using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Kb;

public sealed record PinKbItemToMatterCommand(Guid MatterId, string KbRefKind, Guid KbRefId, string? Note) : IRequest<KbMatterPinDto>;

public sealed class PinKbItemToMatterCommandHandler(IKbMatterPinService service, ICurrentUserService currentUser) : IRequestHandler<PinKbItemToMatterCommand, KbMatterPinDto>
{
    public Task<KbMatterPinDto> Handle(PinKbItemToMatterCommand request, CancellationToken cancellationToken)
        => service.PinAsync(currentUser.TenantId!.Value, request.MatterId, currentUser.UserId, request.KbRefKind, request.KbRefId, request.Note, cancellationToken);
}

public sealed record UnpinKbItemFromMatterCommand(Guid PinId) : IRequest;

public sealed class UnpinKbItemFromMatterCommandHandler(IKbMatterPinService service, ICurrentUserService currentUser) : IRequestHandler<UnpinKbItemFromMatterCommand>
{
    public async Task Handle(UnpinKbItemFromMatterCommand request, CancellationToken cancellationToken)
        => await service.UnpinAsync(currentUser.TenantId!.Value, request.PinId, cancellationToken);
}
