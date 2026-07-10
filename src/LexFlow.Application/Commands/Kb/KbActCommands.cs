using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Kb;

public sealed record CreateKbActCommand(string Name, string? ShortCode, string? Jurisdiction, int? Year) : IRequest<KbActDto>;

public sealed class CreateKbActCommandHandler(IKbActService service, ICurrentUserService currentUser) : IRequestHandler<CreateKbActCommand, KbActDto>
{
    public Task<KbActDto> Handle(CreateKbActCommand request, CancellationToken cancellationToken)
        => service.CreateActAsync(currentUser.TenantId!.Value, request.Name, request.ShortCode, request.Jurisdiction, request.Year, cancellationToken);
}

public sealed record UpdateKbActCommand(Guid Id, string Name, string? ShortCode, string? Jurisdiction, int? Year) : IRequest<KbActDto>;

public sealed class UpdateKbActCommandHandler(IKbActService service, ICurrentUserService currentUser) : IRequestHandler<UpdateKbActCommand, KbActDto>
{
    public Task<KbActDto> Handle(UpdateKbActCommand request, CancellationToken cancellationToken)
        => service.UpdateActAsync(currentUser.TenantId!.Value, request.Id, request.Name, request.ShortCode, request.Jurisdiction, request.Year, cancellationToken);
}

public sealed record CreateKbActSectionCommand(Guid ActId, Guid? ParentId, string Number, string? Title, string? Body, DateOnly? EffectiveFrom) : IRequest<KbActSectionDto>;

public sealed class CreateKbActSectionCommandHandler(IKbActService service, ICurrentUserService currentUser) : IRequestHandler<CreateKbActSectionCommand, KbActSectionDto>
{
    public Task<KbActSectionDto> Handle(CreateKbActSectionCommand request, CancellationToken cancellationToken)
        => service.CreateSectionAsync(currentUser.TenantId!.Value, request.ActId, request.ParentId, request.Number, request.Title, request.Body, request.EffectiveFrom, cancellationToken);
}

public sealed record AmendKbActSectionCommand(Guid SectionId, string? NewTitle, string? NewBody, DateOnly AmendedOn) : IRequest<KbActSectionDto>;

public sealed class AmendKbActSectionCommandHandler(IKbActService service, ICurrentUserService currentUser) : IRequestHandler<AmendKbActSectionCommand, KbActSectionDto>
{
    public Task<KbActSectionDto> Handle(AmendKbActSectionCommand request, CancellationToken cancellationToken)
        => service.AmendSectionAsync(currentUser.TenantId!.Value, request.SectionId, request.NewTitle, request.NewBody, request.AmendedOn, cancellationToken);
}
