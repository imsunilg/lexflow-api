using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.WorkflowRules;

/// <summary>POST /api/v1/workflow-rules.</summary>
public sealed record CreateWorkflowRuleCommand(string Name, string TriggerEvent, string ConditionsJson, string ActionsJson, int RunOrder) : IRequest<WorkflowRuleDto>;

public sealed class CreateWorkflowRuleCommandHandler(IWorkflowRuleService workflowRuleService, ICurrentUserService currentUser) : IRequestHandler<CreateWorkflowRuleCommand, WorkflowRuleDto>
{
    public Task<WorkflowRuleDto> Handle(CreateWorkflowRuleCommand request, CancellationToken cancellationToken)
        => workflowRuleService.CreateAsync(currentUser.TenantId!.Value, request.Name, request.TriggerEvent, request.ConditionsJson, request.ActionsJson, request.RunOrder, cancellationToken);
}

/// <summary>PUT /api/v1/workflow-rules/{id}.</summary>
public sealed record UpdateWorkflowRuleCommand(Guid RuleId, string Name, string TriggerEvent, string ConditionsJson, string ActionsJson, bool Active, int RunOrder) : IRequest<WorkflowRuleDto>;

public sealed class UpdateWorkflowRuleCommandHandler(IWorkflowRuleService workflowRuleService, ICurrentUserService currentUser) : IRequestHandler<UpdateWorkflowRuleCommand, WorkflowRuleDto>
{
    public Task<WorkflowRuleDto> Handle(UpdateWorkflowRuleCommand request, CancellationToken cancellationToken)
        => workflowRuleService.UpdateAsync(currentUser.TenantId!.Value, request.RuleId, request.Name, request.TriggerEvent, request.ConditionsJson, request.ActionsJson, request.Active, request.RunOrder, cancellationToken);
}

/// <summary>DELETE /api/v1/workflow-rules/{id}.</summary>
public sealed record DeleteWorkflowRuleCommand(Guid RuleId) : IRequest;

public sealed class DeleteWorkflowRuleCommandHandler(IWorkflowRuleService workflowRuleService, ICurrentUserService currentUser) : IRequestHandler<DeleteWorkflowRuleCommand>
{
    public async Task Handle(DeleteWorkflowRuleCommand request, CancellationToken cancellationToken)
        => await workflowRuleService.DeleteAsync(currentUser.TenantId!.Value, request.RuleId, cancellationToken);
}

/// <summary>POST /api/v1/workflow-rules/{id}/test {samplePayload} — builder UI's "test with sample record" step.</summary>
public sealed record TestWorkflowRuleCommand(Guid RuleId, string SamplePayloadJson) : IRequest<WorkflowTestResult>;

public sealed class TestWorkflowRuleCommandHandler(IWorkflowRuleService workflowRuleService, ICurrentUserService currentUser) : IRequestHandler<TestWorkflowRuleCommand, WorkflowTestResult>
{
    public Task<WorkflowTestResult> Handle(TestWorkflowRuleCommand request, CancellationToken cancellationToken)
        => workflowRuleService.TestAsync(currentUser.TenantId!.Value, request.RuleId, request.SamplePayloadJson, cancellationToken);
}

/// <summary>POST /api/v1/workflow-rules/seed-defaults — idempotent, application-level seeding of the 12 shipped default rules (§23).</summary>
public sealed record SeedDefaultWorkflowRulesCommand : IRequest<int>;

public sealed class SeedDefaultWorkflowRulesCommandHandler(IWorkflowRuleService workflowRuleService, ICurrentUserService currentUser) : IRequestHandler<SeedDefaultWorkflowRulesCommand, int>
{
    public Task<int> Handle(SeedDefaultWorkflowRulesCommand request, CancellationToken cancellationToken)
        => workflowRuleService.SeedDefaultRulesAsync(currentUser.TenantId!.Value, cancellationToken);
}
