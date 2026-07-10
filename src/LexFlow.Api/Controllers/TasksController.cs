using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Tasks;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Tasks;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 10 (Task Management) — PRD §17.</summary>
[ApiController]
[Route("api/v1/tasks")]
public sealed class TasksController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [RequirePermission("tasks.manage.own")]
    public async Task<IActionResult> Create([FromBody] CreateOpsTaskRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<OpsTaskDto>.Of(await mediator.Send(new CreateOpsTaskCommand(request.Title, request.Description, request.MatterId, request.ClientId, request.OwnerId, request.DueAt, request.Priority, request.Category), cancellationToken)));

    [HttpGet]
    [RequirePermission("tasks.read.own")]
    public async Task<IActionResult> GetAll([FromQuery] Guid? assigneeId, [FromQuery] string? status, [FromQuery] string? priority, [FromQuery] Guid? matterId, [FromQuery] DateTimeOffset? dueFrom, [FromQuery] DateTimeOffset? dueTo, [FromQuery] string? category, [FromQuery] bool? overdue, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<OpsTaskDto>>.Of(await mediator.Send(new GetOpsTasksQuery(assigneeId, status, priority, matterId, dueFrom, dueTo, category, overdue), cancellationToken)));

    [HttpGet("workload")]
    [RequirePermission("tasks.read.team")]
    public async Task<IActionResult> GetWorkload([FromQuery] Guid? teamId, [FromQuery] DateOnly week, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<TaskWorkloadDto>>.Of(await mediator.Send(new GetTaskWorkloadQuery(teamId, week), cancellationToken)));

    /// <summary>AC-TK1: smart parse, e.g. "File written statement for MAT-042 by next Friday @Aditi !high".</summary>
    [HttpPost("parse")]
    [RequirePermission("tasks.manage.own")]
    public async Task<IActionResult> Parse([FromBody] ParseTaskTextRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<ParsedTaskDraft>.Of(await mediator.Send(new ParseTaskTextCommand(request.Text), cancellationToken)));

    [HttpGet("{id:guid}")]
    [RequirePermission("tasks.read.own")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<OpsTaskDto>.Of(await mediator.Send(new GetOpsTaskQuery(id), cancellationToken)));

    [HttpPut("{id:guid}")]
    [RequirePermission("tasks.manage.own")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOpsTaskRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<OpsTaskDto>.Of(await mediator.Send(new UpdateOpsTaskCommand(id, request.Title, request.Description, request.MatterId, request.ClientId, request.OwnerId, request.DueAt, request.Priority, request.Category), cancellationToken)));

    [HttpDelete("{id:guid}")]
    [RequirePermission("tasks.manage.own")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteOpsTaskCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>AC-TK2: 409 if blocked by an open dependency, or if Done is requested with mandatory checklist items unticked.</summary>
    [HttpPost("{id:guid}/status")]
    [RequirePermission("tasks.manage.own")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetOpsTaskStatusRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<OpsTaskDto>.Of(await mediator.Send(new SetOpsTaskStatusCommand(id, request.Status), cancellationToken)));

    [HttpPost("{id:guid}/assignees")]
    [RequirePermission("tasks.assign.others")]
    public async Task<IActionResult> AddAssignee(Guid id, [FromBody] AddTaskAssigneeRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new AddTaskAssigneeCommand(id, request.UserId, request.Role), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/assignees/{userId:guid}")]
    [RequirePermission("tasks.assign.others")]
    public async Task<IActionResult> RemoveAssignee(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        await mediator.Send(new RemoveTaskAssigneeCommand(id, userId), cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/assignees")]
    [RequirePermission("tasks.read.own")]
    public async Task<IActionResult> GetAssignees(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<TaskAssigneeDto>>.Of(await mediator.Send(new GetTaskAssigneesQuery(id), cancellationToken)));

    [HttpPost("{id:guid}/checklist")]
    [RequirePermission("tasks.manage.own")]
    public async Task<IActionResult> AddChecklistItem(Guid id, [FromBody] AddTaskChecklistItemRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<TaskChecklistItemDto>.Of(await mediator.Send(new AddTaskChecklistItemCommand(id, request.Label, request.IsMandatory, request.SortOrder), cancellationToken)));

    [HttpPatch("{id:guid}/checklist/{itemId:guid}")]
    [RequirePermission("tasks.manage.own")]
    public async Task<IActionResult> SetChecklistItemDone(Guid id, Guid itemId, [FromBody] SetChecklistItemDoneRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<TaskChecklistItemDto>.Of(await mediator.Send(new SetTaskChecklistItemDoneCommand(id, itemId, request.IsDone), cancellationToken)));

    [HttpGet("{id:guid}/checklist")]
    [RequirePermission("tasks.read.own")]
    public async Task<IActionResult> GetChecklist(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<TaskChecklistItemDto>>.Of(await mediator.Send(new GetTaskChecklistQuery(id), cancellationToken)));

    [HttpPost("{id:guid}/comments")]
    [RequirePermission("tasks.manage.own")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] AddTaskCommentRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<TaskCommentDto>.Of(await mediator.Send(new AddTaskCommentCommand(id, request.Body), cancellationToken)));

    [HttpGet("{id:guid}/comments")]
    [RequirePermission("tasks.read.own")]
    public async Task<IActionResult> GetComments(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<TaskCommentDto>>.Of(await mediator.Send(new GetTaskCommentsQuery(id), cancellationToken)));

    /// <summary>AC-TK2: a cyclic edge surfaces as 422 CYCLE_DETECTED.</summary>
    [HttpPost("{id:guid}/dependencies")]
    [RequirePermission("tasks.manage.own")]
    public async Task<IActionResult> AddDependency(Guid id, [FromBody] AddTaskDependencyRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new AddTaskDependencyCommand(id, request.DependsOnTaskId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/dependencies/{dependsOnTaskId:guid}")]
    [RequirePermission("tasks.manage.own")]
    public async Task<IActionResult> RemoveDependency(Guid id, Guid dependsOnTaskId, CancellationToken cancellationToken)
    {
        await mediator.Send(new RemoveTaskDependencyCommand(id, dependsOnTaskId), cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/dependencies")]
    [RequirePermission("tasks.read.own")]
    public async Task<IActionResult> GetDependencies(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<Guid>>.Of(await mediator.Send(new GetTaskDependenciesQuery(id), cancellationToken)));
}

/// <summary>Module 10 task-plan templates — PRD §17: POST /api/v1/task-templates, POST /api/v1/matters/{id}/apply-task-template/{templateId}.</summary>
[ApiController]
[Route("api/v1")]
public sealed class TaskTemplatesController(IMediator mediator) : ControllerBase
{
    [HttpPost("task-templates")]
    [RequirePermission("tasks.templates.manage")]
    public async Task<IActionResult> Create([FromBody] CreateTaskTemplateRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<TaskTemplateDto>.Of(await mediator.Send(new CreateTaskTemplateCommand(request.Name, request.MatterType, request.Items), cancellationToken)));

    [HttpGet("task-templates")]
    [RequirePermission("tasks.read.own")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<TaskTemplateDto>>.Of(await mediator.Send(new GetTaskTemplatesQuery(), cancellationToken)));

    /// <summary>AC-TK3: idempotent per (matterId, templateId) — items with a matching TemplateKey are skipped on re-apply.</summary>
    [HttpPost("matters/{id:guid}/apply-task-template/{templateId:guid}")]
    [RequirePermission("tasks.manage.own")]
    public async Task<IActionResult> ApplyToMatter(Guid id, Guid templateId, [FromBody] ApplyTaskTemplateRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<OpsTaskDto>>.Of(await mediator.Send(new ApplyTaskTemplateCommand(id, templateId, request.RelativeFromDate), cancellationToken)));
}

public sealed record CreateOpsTaskRequest(string Title, string? Description, Guid? MatterId, Guid? ClientId, Guid? OwnerId, DateTimeOffset? DueAt, string Priority, string? Category);

public sealed record UpdateOpsTaskRequest(string Title, string? Description, Guid? MatterId, Guid? ClientId, Guid? OwnerId, DateTimeOffset? DueAt, string Priority, string? Category);

public sealed record SetOpsTaskStatusRequest(string Status);

public sealed record AddTaskAssigneeRequest(Guid UserId, string Role);

public sealed record AddTaskChecklistItemRequest(string Label, bool IsMandatory, int SortOrder);

public sealed record SetChecklistItemDoneRequest(bool IsDone);

public sealed record AddTaskCommentRequest(string Body);

public sealed record AddTaskDependencyRequest(Guid DependsOnTaskId);

public sealed record ParseTaskTextRequest(string Text);

public sealed record CreateTaskTemplateRequest(string Name, string? MatterType, IReadOnlyList<TaskTemplateItemInput> Items);

public sealed record ApplyTaskTemplateRequest(DateOnly RelativeFromDate);
