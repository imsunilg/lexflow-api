using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Tasks;

/// <summary>POST /api/v1/tasks.</summary>
public sealed record CreateOpsTaskCommand(string Title, string? Description, Guid? MatterId, Guid? ClientId, Guid? OwnerId, DateTimeOffset? DueAt, string Priority, string? Category) : IRequest<OpsTaskDto>;

public sealed class CreateOpsTaskCommandHandler(ITaskService taskService, ICurrentUserService currentUser) : IRequestHandler<CreateOpsTaskCommand, OpsTaskDto>
{
    public Task<OpsTaskDto> Handle(CreateOpsTaskCommand request, CancellationToken cancellationToken)
        => taskService.CreateAsync(currentUser.TenantId!.Value, currentUser.UserId, new CreateOpsTaskInput(request.Title, request.Description, request.MatterId, request.ClientId, request.OwnerId, request.DueAt, request.Priority, request.Category), cancellationToken);
}

/// <summary>PUT /api/v1/tasks/{id}.</summary>
public sealed record UpdateOpsTaskCommand(Guid TaskId, string Title, string? Description, Guid? MatterId, Guid? ClientId, Guid? OwnerId, DateTimeOffset? DueAt, string Priority, string? Category) : IRequest<OpsTaskDto>;

public sealed class UpdateOpsTaskCommandHandler(ITaskService taskService, ICurrentUserService currentUser) : IRequestHandler<UpdateOpsTaskCommand, OpsTaskDto>
{
    public Task<OpsTaskDto> Handle(UpdateOpsTaskCommand request, CancellationToken cancellationToken)
        => taskService.UpdateAsync(currentUser.TenantId!.Value, request.TaskId, new UpdateOpsTaskInput(request.Title, request.Description, request.MatterId, request.ClientId, request.OwnerId, request.DueAt, request.Priority, request.Category), cancellationToken);
}

/// <summary>DELETE /api/v1/tasks/{id}.</summary>
public sealed record DeleteOpsTaskCommand(Guid TaskId) : IRequest;

public sealed class DeleteOpsTaskCommandHandler(ITaskService taskService, ICurrentUserService currentUser) : IRequestHandler<DeleteOpsTaskCommand>
{
    public async Task Handle(DeleteOpsTaskCommand request, CancellationToken cancellationToken)
        => await taskService.DeleteAsync(currentUser.TenantId!.Value, request.TaskId, cancellationToken);
}

/// <summary>POST /api/v1/tasks/{id}/status. AC-TK2: 409 if blocked by an open dependency; Done additionally requires mandatory checklist items ticked.</summary>
public sealed record SetOpsTaskStatusCommand(Guid TaskId, string Status) : IRequest<OpsTaskDto>;

public sealed class SetOpsTaskStatusCommandHandler(ITaskService taskService, ICurrentUserService currentUser) : IRequestHandler<SetOpsTaskStatusCommand, OpsTaskDto>
{
    public Task<OpsTaskDto> Handle(SetOpsTaskStatusCommand request, CancellationToken cancellationToken)
        => taskService.SetStatusAsync(currentUser.TenantId!.Value, currentUser.UserId, request.TaskId, request.Status, cancellationToken);
}

/// <summary>POST /api/v1/tasks/{id}/assignees.</summary>
public sealed record AddTaskAssigneeCommand(Guid TaskId, Guid UserId, string Role) : IRequest;

public sealed class AddTaskAssigneeCommandHandler(ITaskService taskService, ICurrentUserService currentUser) : IRequestHandler<AddTaskAssigneeCommand>
{
    public async Task Handle(AddTaskAssigneeCommand request, CancellationToken cancellationToken)
        => await taskService.AddAssigneeAsync(currentUser.TenantId!.Value, request.TaskId, request.UserId, request.Role, cancellationToken);
}

/// <summary>DELETE /api/v1/tasks/{id}/assignees/{userId}.</summary>
public sealed record RemoveTaskAssigneeCommand(Guid TaskId, Guid UserId) : IRequest;

public sealed class RemoveTaskAssigneeCommandHandler(ITaskService taskService, ICurrentUserService currentUser) : IRequestHandler<RemoveTaskAssigneeCommand>
{
    public async Task Handle(RemoveTaskAssigneeCommand request, CancellationToken cancellationToken)
        => await taskService.RemoveAssigneeAsync(currentUser.TenantId!.Value, request.TaskId, request.UserId, cancellationToken);
}

/// <summary>POST /api/v1/tasks/{id}/checklist.</summary>
public sealed record AddTaskChecklistItemCommand(Guid TaskId, string Label, bool IsMandatory, int SortOrder) : IRequest<TaskChecklistItemDto>;

public sealed class AddTaskChecklistItemCommandHandler(ITaskService taskService, ICurrentUserService currentUser) : IRequestHandler<AddTaskChecklistItemCommand, TaskChecklistItemDto>
{
    public Task<TaskChecklistItemDto> Handle(AddTaskChecklistItemCommand request, CancellationToken cancellationToken)
        => taskService.AddChecklistItemAsync(currentUser.TenantId!.Value, request.TaskId, request.Label, request.IsMandatory, request.SortOrder, cancellationToken);
}

/// <summary>PATCH /api/v1/tasks/{id}/checklist/{itemId}.</summary>
public sealed record SetTaskChecklistItemDoneCommand(Guid TaskId, Guid ItemId, bool IsDone) : IRequest<TaskChecklistItemDto>;

public sealed class SetTaskChecklistItemDoneCommandHandler(ITaskService taskService, ICurrentUserService currentUser) : IRequestHandler<SetTaskChecklistItemDoneCommand, TaskChecklistItemDto>
{
    public Task<TaskChecklistItemDto> Handle(SetTaskChecklistItemDoneCommand request, CancellationToken cancellationToken)
        => taskService.SetChecklistItemDoneAsync(currentUser.TenantId!.Value, request.TaskId, request.ItemId, request.IsDone, cancellationToken);
}

/// <summary>POST /api/v1/tasks/{id}/comments.</summary>
public sealed record AddTaskCommentCommand(Guid TaskId, string Body) : IRequest<TaskCommentDto>;

public sealed class AddTaskCommentCommandHandler(ITaskService taskService, ICurrentUserService currentUser) : IRequestHandler<AddTaskCommentCommand, TaskCommentDto>
{
    public Task<TaskCommentDto> Handle(AddTaskCommentCommand request, CancellationToken cancellationToken)
        => taskService.AddCommentAsync(currentUser.TenantId!.Value, currentUser.UserId, request.TaskId, request.Body, cancellationToken);
}

/// <summary>POST /api/v1/tasks/{id}/dependencies. AC-TK2: cyclic edge surfaces as 422 CYCLE_DETECTED.</summary>
public sealed record AddTaskDependencyCommand(Guid TaskId, Guid DependsOnTaskId) : IRequest;

public sealed class AddTaskDependencyCommandHandler(ITaskService taskService, ICurrentUserService currentUser) : IRequestHandler<AddTaskDependencyCommand>
{
    public async Task Handle(AddTaskDependencyCommand request, CancellationToken cancellationToken)
        => await taskService.AddDependencyAsync(currentUser.TenantId!.Value, currentUser.UserId, request.TaskId, request.DependsOnTaskId, cancellationToken);
}

/// <summary>DELETE /api/v1/tasks/{id}/dependencies/{dependsOnTaskId}.</summary>
public sealed record RemoveTaskDependencyCommand(Guid TaskId, Guid DependsOnTaskId) : IRequest;

public sealed class RemoveTaskDependencyCommandHandler(ITaskService taskService, ICurrentUserService currentUser) : IRequestHandler<RemoveTaskDependencyCommand>
{
    public async Task Handle(RemoveTaskDependencyCommand request, CancellationToken cancellationToken)
        => await taskService.RemoveDependencyAsync(currentUser.TenantId!.Value, request.TaskId, request.DependsOnTaskId, cancellationToken);
}

/// <summary>POST /api/v1/tasks/parse {text} -> structured draft. AC-TK1.</summary>
public sealed record ParseTaskTextCommand(string Text) : IRequest<ParsedTaskDraft>;

public sealed class ParseTaskTextCommandHandler(ITaskService taskService, ICurrentUserService currentUser) : IRequestHandler<ParseTaskTextCommand, ParsedTaskDraft>
{
    public Task<ParsedTaskDraft> Handle(ParseTaskTextCommand request, CancellationToken cancellationToken)
        => taskService.ParseAsync(currentUser.TenantId!.Value, request.Text, cancellationToken);
}

/// <summary>POST /api/v1/task-templates.</summary>
public sealed record CreateTaskTemplateCommand(string Name, string? MatterType, IReadOnlyList<TaskTemplateItemInput> Items) : IRequest<TaskTemplateDto>;

public sealed class CreateTaskTemplateCommandHandler(ITaskService taskService, ICurrentUserService currentUser) : IRequestHandler<CreateTaskTemplateCommand, TaskTemplateDto>
{
    public Task<TaskTemplateDto> Handle(CreateTaskTemplateCommand request, CancellationToken cancellationToken)
        => taskService.CreateTemplateAsync(currentUser.TenantId!.Value, request.Name, request.MatterType, request.Items, cancellationToken);
}

/// <summary>POST /api/v1/matters/{id}/apply-task-template/{templateId}. AC-TK3: idempotent per (matterId, templateId).</summary>
public sealed record ApplyTaskTemplateCommand(Guid MatterId, Guid TemplateId, DateOnly RelativeFromDate) : IRequest<IReadOnlyList<OpsTaskDto>>;

public sealed class ApplyTaskTemplateCommandHandler(ITaskService taskService, ICurrentUserService currentUser) : IRequestHandler<ApplyTaskTemplateCommand, IReadOnlyList<OpsTaskDto>>
{
    public Task<IReadOnlyList<OpsTaskDto>> Handle(ApplyTaskTemplateCommand request, CancellationToken cancellationToken)
        => taskService.ApplyTemplateToMatterAsync(currentUser.TenantId!.Value, currentUser.UserId, request.MatterId, request.TemplateId, request.RelativeFromDate, cancellationToken);
}
