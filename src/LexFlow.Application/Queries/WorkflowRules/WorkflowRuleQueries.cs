using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.WorkflowRules;

public sealed record GetWorkflowRuleQuery(Guid RuleId) : IRequest<WorkflowRuleDto>;

public sealed class GetWorkflowRuleQueryHandler(IWorkflowRuleService workflowRuleService, ICurrentUserService currentUser) : IRequestHandler<GetWorkflowRuleQuery, WorkflowRuleDto>
{
    public async Task<WorkflowRuleDto> Handle(GetWorkflowRuleQuery request, CancellationToken cancellationToken)
        => await workflowRuleService.GetByIdAsync(currentUser.TenantId!.Value, request.RuleId, cancellationToken)
           ?? throw new NotFoundException("WorkflowRule", request.RuleId);
}

public sealed record GetWorkflowRulesQuery : IRequest<IReadOnlyList<WorkflowRuleDto>>;

public sealed class GetWorkflowRulesQueryHandler(IWorkflowRuleService workflowRuleService, ICurrentUserService currentUser) : IRequestHandler<GetWorkflowRulesQuery, IReadOnlyList<WorkflowRuleDto>>
{
    public Task<IReadOnlyList<WorkflowRuleDto>> Handle(GetWorkflowRulesQuery request, CancellationToken cancellationToken)
        => workflowRuleService.GetAllAsync(currentUser.TenantId!.Value, cancellationToken);
}

public sealed record GetWorkflowRunsQuery(Guid RuleId) : IRequest<IReadOnlyList<WorkflowRunDto>>;

public sealed class GetWorkflowRunsQueryHandler(IWorkflowRuleService workflowRuleService, ICurrentUserService currentUser) : IRequestHandler<GetWorkflowRunsQuery, IReadOnlyList<WorkflowRunDto>>
{
    public Task<IReadOnlyList<WorkflowRunDto>> Handle(GetWorkflowRunsQuery request, CancellationToken cancellationToken)
        => workflowRuleService.GetRunsAsync(currentUser.TenantId!.Value, request.RuleId, cancellationToken);
}
