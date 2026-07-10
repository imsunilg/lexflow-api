using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.NumberSeries;

/// <summary>Module 15 §10 Number Series CRUD (PRD §17).</summary>
public sealed record CreateNumberSeriesCommand(string SeriesKey, int FiscalYear, string FormatPattern, Guid? BranchId) : IRequest<NumberSeriesDto>;

public sealed class CreateNumberSeriesCommandHandler(INumberSeriesService numberSeriesService, ICurrentUserService currentUser)
    : IRequestHandler<CreateNumberSeriesCommand, NumberSeriesDto>
{
    public Task<NumberSeriesDto> Handle(CreateNumberSeriesCommand request, CancellationToken cancellationToken)
        => numberSeriesService.CreateAsync(currentUser.TenantId!.Value, request.SeriesKey, request.FiscalYear, request.FormatPattern, request.BranchId, cancellationToken);
}

public sealed record UpdateNumberSeriesPatternCommand(Guid Id, string FormatPattern) : IRequest<NumberSeriesDto>;

public sealed class UpdateNumberSeriesPatternCommandHandler(INumberSeriesService numberSeriesService, ICurrentUserService currentUser)
    : IRequestHandler<UpdateNumberSeriesPatternCommand, NumberSeriesDto>
{
    public Task<NumberSeriesDto> Handle(UpdateNumberSeriesPatternCommand request, CancellationToken cancellationToken)
        => numberSeriesService.UpdatePatternAsync(currentUser.TenantId!.Value, request.Id, request.FormatPattern, cancellationToken);
}

public sealed record DeleteNumberSeriesCommand(Guid Id) : IRequest;

public sealed class DeleteNumberSeriesCommandHandler(INumberSeriesService numberSeriesService, ICurrentUserService currentUser) : IRequestHandler<DeleteNumberSeriesCommand>
{
    public async Task Handle(DeleteNumberSeriesCommand request, CancellationToken cancellationToken)
        => await numberSeriesService.DeleteAsync(currentUser.TenantId!.Value, request.Id, cancellationToken);
}
