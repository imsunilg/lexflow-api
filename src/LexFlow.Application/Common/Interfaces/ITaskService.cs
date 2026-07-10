namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 10 (Task Management) full vertical slice — status workflow, dependencies
/// (finish-to-start, cycle-checked by the DB trigger), checklists (mandatory-item gate on
/// Done), comments, and matter-type templates (AC-TK3).
/// </summary>
public interface ITaskService
{
    Task<OpsTaskDto> CreateAsync(Guid tenantId, Guid? actorId, CreateOpsTaskInput input, CancellationToken cancellationToken = default);

    Task<OpsTaskDto?> GetByIdAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OpsTaskDto>> GetAllAsync(Guid tenantId, OpsTaskFilter filter, CancellationToken cancellationToken = default);

    Task<OpsTaskDto> UpdateAsync(Guid tenantId, Guid taskId, UpdateOpsTaskInput input, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>AC-TK2: rejects the transition to InProgress/Done with 409 while any predecessor dependency isn't Done; Done additionally requires every mandatory checklist item ticked.</summary>
    Task<OpsTaskDto> SetStatusAsync(Guid tenantId, Guid? actorId, Guid taskId, string status, CancellationToken cancellationToken = default);

    Task AddAssigneeAsync(Guid tenantId, Guid taskId, Guid userId, string role, CancellationToken cancellationToken = default);

    Task RemoveAssigneeAsync(Guid tenantId, Guid taskId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskAssigneeDto>> GetAssigneesAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken = default);

    Task<TaskChecklistItemDto> AddChecklistItemAsync(Guid tenantId, Guid taskId, string label, bool isMandatory, int sortOrder, CancellationToken cancellationToken = default);

    Task<TaskChecklistItemDto> SetChecklistItemDoneAsync(Guid tenantId, Guid taskId, Guid itemId, bool isDone, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskChecklistItemDto>> GetChecklistAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken = default);

    Task<TaskCommentDto> AddCommentAsync(Guid tenantId, Guid? actorId, Guid taskId, string body, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskCommentDto>> GetCommentsAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>Delegates the acyclic-graph check to ops.trg_task_dependencies_check_cycle; a cyclic edge surfaces as DomainRuleException("CYCLE_DETECTED").</summary>
    Task AddDependencyAsync(Guid tenantId, Guid? actorId, Guid taskId, Guid dependsOnTaskId, CancellationToken cancellationToken = default);

    Task RemoveDependencyAsync(Guid tenantId, Guid taskId, Guid dependsOnTaskId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetDependenciesAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>AC-TK1: "File written statement for MAT-042 by next Friday @Aditi !high" -> structured draft (matter lookup by number, assignee by @mention, relative due date, priority marker).</summary>
    Task<ParsedTaskDraft> ParseAsync(Guid tenantId, string text, CancellationToken cancellationToken = default);

    Task<TaskTemplateDto> CreateTemplateAsync(Guid tenantId, string name, string? matterType, IReadOnlyList<TaskTemplateItemInput> items, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskTemplateDto>> GetTemplatesAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>AC-TK3: idempotent per (matterId, templateId) — re-applying skips items already created (TemplateKey match).</summary>
    Task<IReadOnlyList<OpsTaskDto>> ApplyTemplateToMatterAsync(Guid tenantId, Guid? actorId, Guid matterId, Guid templateId, DateOnly relativeFromDate, CancellationToken cancellationToken = default);

    /// <summary>AC-TK5: workload board counts — must match GetAllAsync's filtered counts exactly.</summary>
    Task<IReadOnlyList<TaskWorkloadDto>> GetWorkloadAsync(Guid tenantId, Guid? teamId, DateOnly weekStart, CancellationToken cancellationToken = default);
}

public sealed record CreateOpsTaskInput(string Title, string? Description, Guid? MatterId, Guid? ClientId, Guid? OwnerId, DateTimeOffset? DueAt, string Priority, string? Category);

public sealed record UpdateOpsTaskInput(string Title, string? Description, Guid? MatterId, Guid? ClientId, Guid? OwnerId, DateTimeOffset? DueAt, string Priority, string? Category);

public sealed record OpsTaskFilter(Guid? AssigneeId, string? Status, string? Priority, Guid? MatterId, DateTimeOffset? DueFrom, DateTimeOffset? DueTo, string? Category, bool? OverdueOnly);

public sealed record OpsTaskDto(Guid Id, string? Number, string Title, string? Description, Guid? MatterId, Guid? ClientId, Guid? OwnerId, DateTimeOffset? DueAt, string Priority, string? Category, string Status, int ProgressPct, Guid? RecurrenceId, string? TemplateKey);

public sealed record TaskAssigneeDto(Guid TaskId, Guid UserId, string Role);

public sealed record TaskChecklistItemDto(Guid Id, Guid TaskId, string Label, bool IsDone, bool IsMandatory, int SortOrder);

public sealed record TaskCommentDto(Guid Id, Guid TaskId, Guid? AuthorId, string Body, DateTimeOffset CreatedAt);

public sealed record ParsedTaskDraft(string Title, Guid? MatterId, string? MatterNumber, Guid? AssigneeUserId, string? AssigneeMention, DateTimeOffset? DueAt, string Priority);

public sealed record TaskTemplateItemInput(string Title, int RelativeDueDays, string? Category, int SortOrder, bool IsMandatory);

public sealed record TaskTemplateDto(Guid Id, string Name, string? MatterType, bool IsActive, IReadOnlyList<TaskTemplateItemInput> Items);

public sealed record TaskWorkloadDto(Guid UserId, int New, int InProgress, int InReview, int Done, int Overdue, int Total);
