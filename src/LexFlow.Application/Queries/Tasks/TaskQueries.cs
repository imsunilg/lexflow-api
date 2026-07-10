using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Tasks;

/// <summary>GET /api/v1/tasks/{id}.</summary>
public sealed record GetOpsTaskQuery(Guid TaskId) : IRequest<OpsTaskDto>;

public sealed class GetOpsTaskQueryHandler(ITaskService taskService, ICurrentUserService currentUser) : IRequestHandler<GetOpsTaskQuery, OpsTaskDto>
{
    public async Task<OpsTaskDto> Handle(GetOpsTaskQuery request, CancellationToken cancellationToken)
        => await taskService.GetByIdAsync(currentUser.TenantId!.Value, request.TaskId, cancellationToken)
           ?? throw new NotFoundException("OpsTask", request.TaskId);
}

/// <summary>GET /api/v1/tasks (filters: assignee,status,priority,matter,due range,category,overdue).</summary>
public sealed record GetOpsTasksQuery(Guid? AssigneeId, string? Status, string? Priority, Guid? MatterId, DateTimeOffset? DueFrom, DateTimeOffset? DueTo, string? Category, bool? OverdueOnly) : IRequest<IReadOnlyList<OpsTaskDto>>;

public sealed class GetOpsTasksQueryHandler(ITaskService taskService, ICurrentUserService currentUser) : IRequestHandler<GetOpsTasksQuery, IReadOnlyList<OpsTaskDto>>
{
    public Task<IReadOnlyList<OpsTaskDto>> Handle(GetOpsTasksQuery request, CancellationToken cancellationToken)
        => taskService.GetAllAsync(currentUser.TenantId!.Value, new OpsTaskFilter(request.AssigneeId, request.Status, request.Priority, request.MatterId, request.DueFrom, request.DueTo, request.Category, request.OverdueOnly), cancellationToken);
}

public sealed record GetTaskAssigneesQuery(Guid TaskId) : IRequest<IReadOnlyList<TaskAssigneeDto>>;

public sealed class GetTaskAssigneesQueryHandler(ITaskService taskService, ICurrentUserService currentUser) : IRequestHandler<GetTaskAssigneesQuery, IReadOnlyList<TaskAssigneeDto>>
{
    public Task<IReadOnlyList<TaskAssigneeDto>> Handle(GetTaskAssigneesQuery request, CancellationToken cancellationToken)
        => taskService.GetAssigneesAsync(currentUser.TenantId!.Value, request.TaskId, cancellationToken);
}

public sealed record GetTaskChecklistQuery(Guid TaskId) : IRequest<IReadOnlyList<TaskChecklistItemDto>>;

public sealed class GetTaskChecklistQueryHandler(ITaskService taskService, ICurrentUserService currentUser) : IRequestHandler<GetTaskChecklistQuery, IReadOnlyList<TaskChecklistItemDto>>
{
    public Task<IReadOnlyList<TaskChecklistItemDto>> Handle(GetTaskChecklistQuery request, CancellationToken cancellationToken)
        => taskService.GetChecklistAsync(currentUser.TenantId!.Value, request.TaskId, cancellationToken);
}

public sealed record GetTaskCommentsQuery(Guid TaskId) : IRequest<IReadOnlyList<TaskCommentDto>>;

public sealed class GetTaskCommentsQueryHandler(ITaskService taskService, ICurrentUserService currentUser) : IRequestHandler<GetTaskCommentsQuery, IReadOnlyList<TaskCommentDto>>
{
    public Task<IReadOnlyList<TaskCommentDto>> Handle(GetTaskCommentsQuery request, CancellationToken cancellationToken)
        => taskService.GetCommentsAsync(currentUser.TenantId!.Value, request.TaskId, cancellationToken);
}

public sealed record GetTaskDependenciesQuery(Guid TaskId) : IRequest<IReadOnlyList<Guid>>;

public sealed class GetTaskDependenciesQueryHandler(ITaskService taskService, ICurrentUserService currentUser) : IRequestHandler<GetTaskDependenciesQuery, IReadOnlyList<Guid>>
{
    public Task<IReadOnlyList<Guid>> Handle(GetTaskDependenciesQuery request, CancellationToken cancellationToken)
        => taskService.GetDependenciesAsync(currentUser.TenantId!.Value, request.TaskId, cancellationToken);
}

/// <summary>GET /api/v1/task-templates.</summary>
public sealed record GetTaskTemplatesQuery : IRequest<IReadOnlyList<TaskTemplateDto>>;

public sealed class GetTaskTemplatesQueryHandler(ITaskService taskService, ICurrentUserService currentUser) : IRequestHandler<GetTaskTemplatesQuery, IReadOnlyList<TaskTemplateDto>>
{
    public Task<IReadOnlyList<TaskTemplateDto>> Handle(GetTaskTemplatesQuery request, CancellationToken cancellationToken)
        => taskService.GetTemplatesAsync(currentUser.TenantId!.Value, cancellationToken);
}

/// <summary>GET /api/v1/tasks/workload?teamId=&amp;week=. AC-TK5.</summary>
public sealed record GetTaskWorkloadQuery(Guid? TeamId, DateOnly WeekStart) : IRequest<IReadOnlyList<TaskWorkloadDto>>;

public sealed class GetTaskWorkloadQueryHandler(ITaskService taskService, ICurrentUserService currentUser) : IRequestHandler<GetTaskWorkloadQuery, IReadOnlyList<TaskWorkloadDto>>
{
    public Task<IReadOnlyList<TaskWorkloadDto>> Handle(GetTaskWorkloadQuery request, CancellationToken cancellationToken)
        => taskService.GetWorkloadAsync(currentUser.TenantId!.Value, request.TeamId, request.WeekStart, cancellationToken);
}
