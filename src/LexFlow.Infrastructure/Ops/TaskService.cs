using System.Text.RegularExpressions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Ops;

/// <summary>
/// Module 10 (Task Management) full vertical slice. Dependency cycle-checking is
/// delegated entirely to ops.trg_task_dependencies_check_cycle (a BEFORE INSERT trigger on
/// ops.task_dependencies) — this service only inserts the edge and translates the
/// trigger's raised exception into DomainRuleException("CYCLE_DETECTED", ...); it does not
/// re-implement the DFS check itself.
/// </summary>
public sealed partial class TaskService(LexFlowDbContext db) : ITaskService
{
    private static readonly string[] StatusOrder = ["New", "InProgress", "InReview", "Done", "Cancelled"];

    public async Task<OpsTaskDto> CreateAsync(Guid tenantId, Guid? actorId, CreateOpsTaskInput input, CancellationToken cancellationToken = default)
    {
        var task = new OpsTask(tenantId, input.Title, input.Description, input.MatterId, input.ClientId, input.OwnerId, input.DueAt, input.Priority, input.Category);
        await db.OpsTasks.AddAsync(task, cancellationToken);

        if (input.OwnerId.HasValue)
        {
            await db.TaskAssignees.AddAsync(new TaskAssignee(tenantId, task.Id, input.OwnerId.Value, "owner"), cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(task);
    }

    public async Task<OpsTaskDto?> GetByIdAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = await db.OpsTasks.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Id == taskId, cancellationToken);
        return task is null ? null : ToDto(task);
    }

    public async Task<IReadOnlyList<OpsTaskDto>> GetAllAsync(Guid tenantId, OpsTaskFilter filter, CancellationToken cancellationToken = default)
    {
        var query = db.OpsTasks.Where(t => t.TenantId == tenantId).AsQueryable();

        if (filter.AssigneeId.HasValue)
        {
            var assignedTaskIds = db.TaskAssignees.Where(a => a.TenantId == tenantId && a.UserId == filter.AssigneeId.Value).Select(a => a.TaskId);
            query = query.Where(t => t.OwnerId == filter.AssigneeId.Value || assignedTaskIds.Contains(t.Id));
        }

        if (filter.Status is not null)
        {
            query = query.Where(t => t.Status == filter.Status);
        }

        if (filter.Priority is not null)
        {
            query = query.Where(t => t.Priority == filter.Priority);
        }

        if (filter.MatterId.HasValue)
        {
            query = query.Where(t => t.MatterId == filter.MatterId.Value);
        }

        if (filter.Category is not null)
        {
            query = query.Where(t => t.Category == filter.Category);
        }

        if (filter.DueFrom.HasValue)
        {
            query = query.Where(t => t.DueAt >= filter.DueFrom.Value);
        }

        if (filter.DueTo.HasValue)
        {
            query = query.Where(t => t.DueAt <= filter.DueTo.Value);
        }

        if (filter.OverdueOnly == true)
        {
            var now = DateTimeOffset.UtcNow;
            query = query.Where(t => t.DueAt < now && t.Status != "Done" && t.Status != "Cancelled");
        }

        var tasks = await query.OrderBy(t => t.DueAt).ToListAsync(cancellationToken);
        return tasks.Select(ToDto).ToList();
    }

    public async Task<OpsTaskDto> UpdateAsync(Guid tenantId, Guid taskId, UpdateOpsTaskInput input, CancellationToken cancellationToken = default)
    {
        var task = await GetOrThrowAsync(tenantId, taskId, cancellationToken);
        task.UpdateDetails(input.Title, input.Description, input.MatterId, input.ClientId, input.OwnerId, input.DueAt, input.Priority, input.Category);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(task);
    }

    public async Task DeleteAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = await GetOrThrowAsync(tenantId, taskId, cancellationToken);
        db.OpsTasks.Remove(task);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<OpsTaskDto> SetStatusAsync(Guid tenantId, Guid? actorId, Guid taskId, string status, CancellationToken cancellationToken = default)
    {
        if (!StatusOrder.Contains(status))
        {
            throw new Application.Common.Exceptions.ValidationException([new FluentValidation.Results.ValidationFailure("status", $"'{status}' is not a valid task status.")]);
        }

        var task = await GetOrThrowAsync(tenantId, taskId, cancellationToken);

        if (status is "InProgress" or "Done")
        {
            var blocked = await IsBlockedByOpenDependencyAsync(tenantId, taskId, cancellationToken);
            if (blocked)
            {
                throw new ConflictException("This task is blocked by an incomplete predecessor dependency.", "TASK_BLOCKED");
            }
        }

        if (status == "Done")
        {
            var incompleteMandatory = await db.TaskChecklistItems
                .Where(c => c.TenantId == tenantId && c.TaskId == taskId && c.IsMandatory && !c.IsDone)
                .AnyAsync(cancellationToken);
            if (incompleteMandatory)
            {
                throw new ConflictException("All mandatory checklist items must be ticked before this task can be marked Done.", "CHECKLIST_INCOMPLETE");
            }

            task.SetProgressPct(100);
        }

        task.SetStatus(status);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(task);
    }

    private async Task<bool> IsBlockedByOpenDependencyAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken)
    {
        var dependsOnIds = await db.TaskDependencies
            .Where(d => d.TenantId == tenantId && d.TaskId == taskId)
            .Select(d => d.DependsOnTaskId)
            .ToListAsync(cancellationToken);

        if (dependsOnIds.Count == 0)
        {
            return false;
        }

        return await db.OpsTasks.AnyAsync(t => dependsOnIds.Contains(t.Id) && t.Status != "Done", cancellationToken);
    }

    public async Task AddAssigneeAsync(Guid tenantId, Guid taskId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        await GetOrThrowAsync(tenantId, taskId, cancellationToken);
        var existing = await db.TaskAssignees.SingleOrDefaultAsync(a => a.TenantId == tenantId && a.TaskId == taskId && a.UserId == userId, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        await db.TaskAssignees.AddAsync(new TaskAssignee(tenantId, taskId, userId, role), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAssigneeAsync(Guid tenantId, Guid taskId, Guid userId, CancellationToken cancellationToken = default)
    {
        var assignee = await db.TaskAssignees.SingleOrDefaultAsync(a => a.TenantId == tenantId && a.TaskId == taskId && a.UserId == userId, cancellationToken);
        if (assignee is not null)
        {
            db.TaskAssignees.Remove(assignee);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<TaskAssigneeDto>> GetAssigneesAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken = default)
    {
        var assignees = await db.TaskAssignees.Where(a => a.TenantId == tenantId && a.TaskId == taskId).ToListAsync(cancellationToken);
        return assignees.Select(a => new TaskAssigneeDto(a.TaskId, a.UserId, a.Role)).ToList();
    }

    public async Task<TaskChecklistItemDto> AddChecklistItemAsync(Guid tenantId, Guid taskId, string label, bool isMandatory, int sortOrder, CancellationToken cancellationToken = default)
    {
        await GetOrThrowAsync(tenantId, taskId, cancellationToken);
        var item = new TaskChecklistItem(tenantId, taskId, label, isMandatory, sortOrder);
        await db.TaskChecklistItems.AddAsync(item, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(item);
    }

    public async Task<TaskChecklistItemDto> SetChecklistItemDoneAsync(Guid tenantId, Guid taskId, Guid itemId, bool isDone, CancellationToken cancellationToken = default)
    {
        var item = await db.TaskChecklistItems.SingleOrDefaultAsync(c => c.TenantId == tenantId && c.TaskId == taskId && c.Id == itemId, cancellationToken)
            ?? throw new NotFoundException(nameof(TaskChecklistItem), itemId);
        item.SetDone(isDone);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(item);
    }

    public async Task<IReadOnlyList<TaskChecklistItemDto>> GetChecklistAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken = default)
    {
        var items = await db.TaskChecklistItems.Where(c => c.TenantId == tenantId && c.TaskId == taskId).OrderBy(c => c.SortOrder).ToListAsync(cancellationToken);
        return items.Select(ToDto).ToList();
    }

    public async Task<TaskCommentDto> AddCommentAsync(Guid tenantId, Guid? actorId, Guid taskId, string body, CancellationToken cancellationToken = default)
    {
        await GetOrThrowAsync(tenantId, taskId, cancellationToken);
        var comment = new TaskComment(tenantId, taskId, actorId, body);
        await db.TaskComments.AddAsync(comment, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(comment);
    }

    public async Task<IReadOnlyList<TaskCommentDto>> GetCommentsAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken = default)
    {
        var comments = await db.TaskComments.Where(c => c.TenantId == tenantId && c.TaskId == taskId).OrderBy(c => c.CreatedAt).ToListAsync(cancellationToken);
        return comments.Select(ToDto).ToList();
    }

    public async Task AddDependencyAsync(Guid tenantId, Guid? actorId, Guid taskId, Guid dependsOnTaskId, CancellationToken cancellationToken = default)
    {
        if (taskId == dependsOnTaskId)
        {
            throw new DomainRuleException("CYCLE_DETECTED", "A task cannot depend on itself.");
        }

        await db.TaskDependencies.AddAsync(new TaskDependency(tenantId, taskId, dependsOnTaskId), cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsCycleDetectedError(ex))
        {
            throw new DomainRuleException("CYCLE_DETECTED", $"Adding dependency {taskId} -> {dependsOnTaskId} would create a cycle.");
        }
    }

    /// <summary>Public and static so the outbox-trigger-translation logic is directly unit-testable without a real Postgres connection.</summary>
    public static bool IsCycleDetectedError(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("CYCLE_DETECTED", StringComparison.OrdinalIgnoreCase) == true;

    public async Task RemoveDependencyAsync(Guid tenantId, Guid taskId, Guid dependsOnTaskId, CancellationToken cancellationToken = default)
    {
        var dependency = await db.TaskDependencies.SingleOrDefaultAsync(d => d.TenantId == tenantId && d.TaskId == taskId && d.DependsOnTaskId == dependsOnTaskId, cancellationToken);
        if (dependency is not null)
        {
            db.TaskDependencies.Remove(dependency);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<Guid>> GetDependenciesAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken = default)
        => await db.TaskDependencies.Where(d => d.TenantId == tenantId && d.TaskId == taskId).Select(d => d.DependsOnTaskId).ToListAsync(cancellationToken);

    public async Task<TaskTemplateDto> CreateTemplateAsync(Guid tenantId, string name, string? matterType, IReadOnlyList<TaskTemplateItemInput> items, CancellationToken cancellationToken = default)
    {
        var template = new TaskTemplate(tenantId, name, matterType);
        await db.TaskTemplates.AddAsync(template, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var item in items)
        {
            await db.TaskTemplateItems.AddAsync(new TaskTemplateItem(tenantId, template.Id, item.Title, item.RelativeDueDays, item.Category, item.SortOrder, item.IsMandatory), cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(template, cancellationToken);
    }

    public async Task<IReadOnlyList<TaskTemplateDto>> GetTemplatesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var templates = await db.TaskTemplates.Where(t => t.TenantId == tenantId).OrderBy(t => t.Name).ToListAsync(cancellationToken);
        var result = new List<TaskTemplateDto>();
        foreach (var template in templates)
        {
            result.Add(await ToDtoAsync(template, cancellationToken));
        }

        return result;
    }

    public async Task<IReadOnlyList<OpsTaskDto>> ApplyTemplateToMatterAsync(Guid tenantId, Guid? actorId, Guid matterId, Guid templateId, DateOnly relativeFromDate, CancellationToken cancellationToken = default)
    {
        var template = await db.TaskTemplates.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Id == templateId, cancellationToken)
            ?? throw new NotFoundException(nameof(TaskTemplate), templateId);
        var items = await db.TaskTemplateItems.Where(i => i.TenantId == tenantId && i.TemplateId == templateId).OrderBy(i => i.SortOrder).ToListAsync(cancellationToken);

        var created = new List<OpsTask>();
        foreach (var item in items)
        {
            var templateKey = $"{templateId:N}:{item.Id:N}";
            var alreadyApplied = await db.OpsTasks.AnyAsync(t => t.TenantId == tenantId && t.MatterId == matterId && t.TemplateKey == templateKey, cancellationToken);
            if (alreadyApplied)
            {
                continue;
            }

            var dueAt = new DateTimeOffset(relativeFromDate.AddDays(item.RelativeDueDays).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var task = new OpsTask(tenantId, item.Title, description: null, matterId, clientId: null, ownerId: null, dueAt, "Medium", item.Category, recurrenceId: null, templateKey);
            await db.OpsTasks.AddAsync(task, cancellationToken);
            created.Add(task);

            if (item.IsMandatory)
            {
                await db.TaskChecklistItems.AddAsync(new TaskChecklistItem(tenantId, task.Id, "Complete per matter template", isMandatory: true, sortOrder: 0), cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return created.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<TaskWorkloadDto>> GetWorkloadAsync(Guid tenantId, Guid? teamId, DateOnly weekStart, CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<Guid>? teamUserIds = null;
        if (teamId.HasValue)
        {
            teamUserIds = await db.TeamMembers.Where(m => m.TenantId == tenantId && m.TeamId == teamId.Value).Select(m => m.UserId).ToListAsync(cancellationToken);
        }

        var weekEnd = weekStart.AddDays(7);
        var weekStartOffset = new DateTimeOffset(weekStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var weekEndOffset = new DateTimeOffset(weekEnd.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var now = DateTimeOffset.UtcNow;

        var query = db.OpsTasks.Where(t => t.TenantId == tenantId && t.OwnerId != null && t.DueAt >= weekStartOffset && t.DueAt < weekEndOffset);
        if (teamUserIds is not null)
        {
            query = query.Where(t => teamUserIds.Contains(t.OwnerId!.Value));
        }

        var tasks = await query.ToListAsync(cancellationToken);

        return tasks
            .GroupBy(t => t.OwnerId!.Value)
            .Select(g => new TaskWorkloadDto(
                g.Key,
                g.Count(t => t.Status == "New"),
                g.Count(t => t.Status == "InProgress"),
                g.Count(t => t.Status == "InReview"),
                g.Count(t => t.Status == "Done"),
                g.Count(t => t.DueAt < now && t.Status is not ("Done" or "Cancelled")),
                g.Count()))
            .ToList();
    }

    private async Task<OpsTask> GetOrThrowAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken)
        => await db.OpsTasks.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Id == taskId, cancellationToken)
            ?? throw new NotFoundException(nameof(OpsTask), taskId);

    private async Task<TaskTemplateDto> ToDtoAsync(TaskTemplate template, CancellationToken cancellationToken)
    {
        var items = await db.TaskTemplateItems.Where(i => i.TenantId == template.TenantId && i.TemplateId == template.Id).OrderBy(i => i.SortOrder).ToListAsync(cancellationToken);
        return new TaskTemplateDto(template.Id, template.Name, template.MatterType, template.IsActive, items.Select(i => new TaskTemplateItemInput(i.Title, i.RelativeDueDays, i.Category, i.SortOrder, i.IsMandatory)).ToList());
    }

    private static OpsTaskDto ToDto(OpsTask t) => new(t.Id, t.Number, t.Title, t.Description, t.MatterId, t.ClientId, t.OwnerId, t.DueAt, t.Priority, t.Category, t.Status, t.ProgressPct, t.RecurrenceId, t.TemplateKey);

    private static TaskChecklistItemDto ToDto(TaskChecklistItem c) => new(c.Id, c.TaskId, c.Label, c.IsDone, c.IsMandatory, c.SortOrder);

    private static TaskCommentDto ToDto(TaskComment c) => new(c.Id, c.TaskId, c.AuthorId, c.Body, c.CreatedAt);
}
