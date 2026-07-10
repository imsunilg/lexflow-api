namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Minimal slice of the future Module 10 task-management service — just enough for
/// Module 5's "one-click create compliance task" from a hearing outcome (C-7 in the
/// Build Playbook owns the full Task vertical slice: checklists, dependencies,
/// comments, templates, which are out of scope here).
/// </summary>
public interface IOpsTaskService
{
    Task<Guid> CreateComplianceTaskAsync(Guid tenantId, Guid? actorId, Guid matterId, string title, string? description, DateTimeOffset? dueAt, Guid? ownerId, CancellationToken cancellationToken = default);
}
