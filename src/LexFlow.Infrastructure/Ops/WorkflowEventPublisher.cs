using System.Text.Json;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;

namespace LexFlow.Infrastructure.Ops;

/// <summary>See IWorkflowEventPublisher: adds the outbox row but leaves SaveChangesAsync to the caller's own unit of work.</summary>
public sealed class WorkflowEventPublisher(LexFlowDbContext db) : IWorkflowEventPublisher
{
    public async Task PublishAsync(Guid tenantId, string eventType, Guid? entityId, object payload, CancellationToken cancellationToken = default)
    {
        var payloadJson = JsonSerializer.Serialize(payload);
        await db.WorkflowEventOutbox.AddAsync(new WorkflowEventOutbox(tenantId, eventType, entityId, payloadJson), cancellationToken);
    }
}
