using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Reports;

public sealed record GetReportCatalogQuery : IRequest<IReadOnlyList<ReportCatalogItem>>;

public sealed class GetReportCatalogQueryHandler(IStandardReportService service) : IRequestHandler<GetReportCatalogQuery, IReadOnlyList<ReportCatalogItem>>
{
    public Task<IReadOnlyList<ReportCatalogItem>> Handle(GetReportCatalogQuery request, CancellationToken cancellationToken)
        => Task.FromResult(service.GetCatalog());
}

public sealed record GetReportRunQuery(Guid RunId) : IRequest<ReportRunDto?>;

public sealed class GetReportRunQueryHandler(IReportRunService service, ICurrentUserService currentUser) : IRequestHandler<GetReportRunQuery, ReportRunDto?>
{
    public Task<ReportRunDto?> Handle(GetReportRunQuery request, CancellationToken cancellationToken)
        => service.GetRunAsync(currentUser.TenantId!.Value, request.RunId, cancellationToken);
}

public sealed record GetCustomReportsQuery : IRequest<IReadOnlyList<ReportDefinitionDto>>;

public sealed class GetCustomReportsQueryHandler(ICustomReportService service, ICurrentUserService currentUser) : IRequestHandler<GetCustomReportsQuery, IReadOnlyList<ReportDefinitionDto>>
{
    public Task<IReadOnlyList<ReportDefinitionDto>> Handle(GetCustomReportsQuery request, CancellationToken cancellationToken)
        => service.GetForOwnerAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, cancellationToken);
}

public sealed record GetCustomReportQuery(Guid Id) : IRequest<ReportDefinitionDto?>;

public sealed class GetCustomReportQueryHandler(ICustomReportService service, ICurrentUserService currentUser) : IRequestHandler<GetCustomReportQuery, ReportDefinitionDto?>
{
    public Task<ReportDefinitionDto?> Handle(GetCustomReportQuery request, CancellationToken cancellationToken)
        => service.GetAsync(currentUser.TenantId!.Value, request.Id, cancellationToken);
}

public sealed record GetReportSchedulesQuery : IRequest<IReadOnlyList<ReportScheduleDto>>;

public sealed class GetReportSchedulesQueryHandler(IReportSchedulerService service, ICurrentUserService currentUser) : IRequestHandler<GetReportSchedulesQuery, IReadOnlyList<ReportScheduleDto>>
{
    public Task<IReadOnlyList<ReportScheduleDto>> Handle(GetReportSchedulesQuery request, CancellationToken cancellationToken)
        => service.GetForOwnerAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, cancellationToken);
}
