using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Dashboard;

/// <summary>PUT /api/v1/dashboard/layout.</summary>
public sealed record SaveDashboardLayoutCommand(DashboardLayoutDto Layout) : IRequest<DashboardLayoutDto>;

public sealed class SaveDashboardLayoutCommandHandler(IDashboardService dashboardService, ICurrentUserService currentUser) : IRequestHandler<SaveDashboardLayoutCommand, DashboardLayoutDto>
{
    public Task<DashboardLayoutDto> Handle(SaveDashboardLayoutCommand request, CancellationToken cancellationToken)
        => dashboardService.SaveLayoutAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.Layout, cancellationToken);
}

/// <summary>POST /api/v1/dashboard/widgets/activity/clear.</summary>
public sealed record ClearActivityCommand : IRequest;

public sealed class ClearActivityCommandHandler(IDashboardService dashboardService, ICurrentUserService currentUser) : IRequestHandler<ClearActivityCommand>
{
    public Task Handle(ClearActivityCommand request, CancellationToken cancellationToken)
        => dashboardService.ClearActivityAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, cancellationToken);
}
