using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Dunning;

public sealed record MuteDunningCommand(Guid InvoiceId) : IRequest;

public sealed class MuteDunningCommandHandler(IDunningService service, ICurrentUserService currentUser) : IRequestHandler<MuteDunningCommand>
{
    public async Task Handle(MuteDunningCommand request, CancellationToken cancellationToken)
        => await service.MuteAsync(currentUser.TenantId!.Value, request.InvoiceId, cancellationToken);
}

public sealed record UpsertDunningScheduleCommand(string Name, string StepsJson, bool IsActive) : IRequest<DunningScheduleDto>;

public sealed class UpsertDunningScheduleCommandHandler(IDunningService service, ICurrentUserService currentUser) : IRequestHandler<UpsertDunningScheduleCommand, DunningScheduleDto>
{
    public Task<DunningScheduleDto> Handle(UpsertDunningScheduleCommand request, CancellationToken cancellationToken)
        => service.UpsertScheduleAsync(currentUser.TenantId!.Value, request.Name, request.StepsJson, request.IsActive, cancellationToken);
}
