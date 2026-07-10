namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// §23: "event -> outbox -> Service Bus -> workflow engine worker." Call from within an
/// existing unit of work (adds a row via the ambient LexFlowDbContext but does not call
/// SaveChangesAsync itself) so the outbox row commits in the same transaction as the
/// domain mutation that raised the event — same same-transaction guarantee as
/// IDocumentIndexer's outbox in Module 7.
/// </summary>
public interface IWorkflowEventPublisher
{
    Task PublishAsync(Guid tenantId, string eventType, Guid? entityId, object payload, CancellationToken cancellationToken = default);
}
