using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.WorkflowRules;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.WorkflowRules;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>§23 Workflow Rules (Automation Engine) builder UI backend.</summary>
[ApiController]
[Route("api/v1/workflow-rules")]
public sealed class WorkflowRulesController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [RequirePermission("workflow.rules.manage")]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowRuleRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<WorkflowRuleDto>.Of(await mediator.Send(new CreateWorkflowRuleCommand(request.Name, request.TriggerEvent, request.ConditionsJson, request.ActionsJson, request.RunOrder), cancellationToken)));

    [HttpGet]
    [RequirePermission("workflow.rules.read")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<WorkflowRuleDto>>.Of(await mediator.Send(new GetWorkflowRulesQuery(), cancellationToken)));

    [HttpGet("{id:guid}")]
    [RequirePermission("workflow.rules.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<WorkflowRuleDto>.Of(await mediator.Send(new GetWorkflowRuleQuery(id), cancellationToken)));

    [HttpPut("{id:guid}")]
    [RequirePermission("workflow.rules.manage")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkflowRuleRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<WorkflowRuleDto>.Of(await mediator.Send(new UpdateWorkflowRuleCommand(id, request.Name, request.TriggerEvent, request.ConditionsJson, request.ActionsJson, request.Active, request.RunOrder), cancellationToken)));

    [HttpDelete("{id:guid}")]
    [RequirePermission("workflow.rules.manage")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteWorkflowRuleCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>Builder UI's "test with sample record" step — runs conditions+actions against a caller-supplied payload without touching the outbox.</summary>
    [HttpPost("{id:guid}/test")]
    [RequirePermission("workflow.rules.manage")]
    public async Task<IActionResult> Test(Guid id, [FromBody] TestWorkflowRuleRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<WorkflowTestResult>.Of(await mediator.Send(new TestWorkflowRuleCommand(id, request.SamplePayloadJson), cancellationToken)));

    [HttpGet("{id:guid}/runs")]
    [RequirePermission("workflow.rules.read")]
    public async Task<IActionResult> GetRuns(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<WorkflowRunDto>>.Of(await mediator.Send(new GetWorkflowRunsQuery(id), cancellationToken)));

    /// <summary>Idempotent, application-level (not raw SQL) seeding of the 12 shipped default rules (§23).</summary>
    [HttpPost("seed-defaults")]
    [RequirePermission("workflow.rules.manage")]
    public async Task<IActionResult> SeedDefaults(CancellationToken cancellationToken)
        => Ok(ApiResponse<int>.Of(await mediator.Send(new SeedDefaultWorkflowRulesCommand(), cancellationToken)));
}

public sealed record CreateWorkflowRuleRequest(string Name, string TriggerEvent, string ConditionsJson, string ActionsJson, int RunOrder);

public sealed record UpdateWorkflowRuleRequest(string Name, string TriggerEvent, string ConditionsJson, string ActionsJson, bool Active, int RunOrder);

public sealed record TestWorkflowRuleRequest(string SamplePayloadJson);
