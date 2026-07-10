using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;

namespace LexFlow.Infrastructure.Legal;

/// <summary>Minimal ops.tasks writer — see IOpsTaskService for scope.</summary>
public sealed class OpsTaskService(LexFlowDbContext db) : IOpsTaskService
{
    public async Task<Guid> CreateComplianceTaskAsync(Guid tenantId, Guid? actorId, Guid matterId, string title, string? description, DateTimeOffset? dueAt, Guid? ownerId, CancellationToken cancellationToken = default)
    {
        var task = new OpsTask(tenantId, title, description, matterId, clientId: null, ownerId, dueAt, "Medium", "Compliance");
        await db.OpsTasks.AddAsync(task, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return task.Id;
    }
}
