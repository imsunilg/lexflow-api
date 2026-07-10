using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Reports;

public sealed record RunStandardReportCommand(string ReportKey, ReportRunParams Params) : IRequest<ReportRunOutcome>;

public sealed class RunStandardReportCommandHandler(IReportRunService service, ICurrentUserService currentUser) : IRequestHandler<RunStandardReportCommand, ReportRunOutcome>
{
    public Task<ReportRunOutcome> Handle(RunStandardReportCommand request, CancellationToken cancellationToken)
        => service.RunStandardAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.ReportKey, request.Params, cancellationToken);
}

public sealed record RunCustomReportCommand(Guid DefinitionId) : IRequest<ReportRunOutcome>;

public sealed class RunCustomReportCommandHandler(IReportRunService service, ICurrentUserService currentUser) : IRequestHandler<RunCustomReportCommand, ReportRunOutcome>
{
    public Task<ReportRunOutcome> Handle(RunCustomReportCommand request, CancellationToken cancellationToken)
        => service.RunCustomAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.DefinitionId, cancellationToken);
}

public sealed record CreateCustomReportCommand(CustomReportDefinitionInput Input) : IRequest<ReportDefinitionDto>;

public sealed class CreateCustomReportCommandHandler(ICustomReportService service, ICurrentUserService currentUser) : IRequestHandler<CreateCustomReportCommand, ReportDefinitionDto>
{
    public Task<ReportDefinitionDto> Handle(CreateCustomReportCommand request, CancellationToken cancellationToken)
        => service.CreateAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.Input, cancellationToken);
}

public sealed record UpdateCustomReportCommand(Guid Id, CustomReportDefinitionInput Input) : IRequest<ReportDefinitionDto>;

public sealed class UpdateCustomReportCommandHandler(ICustomReportService service, ICurrentUserService currentUser) : IRequestHandler<UpdateCustomReportCommand, ReportDefinitionDto>
{
    public Task<ReportDefinitionDto> Handle(UpdateCustomReportCommand request, CancellationToken cancellationToken)
        => service.UpdateAsync(currentUser.TenantId!.Value, request.Id, request.Input, cancellationToken);
}

public sealed record CreateReportScheduleCommand(ReportScheduleInput Input) : IRequest<ReportScheduleDto>;

public sealed class CreateReportScheduleCommandHandler(IReportSchedulerService service, ICurrentUserService currentUser) : IRequestHandler<CreateReportScheduleCommand, ReportScheduleDto>
{
    public Task<ReportScheduleDto> Handle(CreateReportScheduleCommand request, CancellationToken cancellationToken)
        => service.CreateAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.Input, cancellationToken);
}
