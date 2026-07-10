namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// §23 "wait (duration/until-date) enabling multi-step sequences": a rule's action list is
/// executed in order; hitting a "wait" action schedules the remaining actions to resume via
/// Hangfire after the configured delay rather than executing them inline. Depth is the
/// wait-continuation count so far (chain depth <= 5 per §23).
/// </summary>
public interface IWorkflowActionResumer
{
    Task ResumeAsync(Guid tenantId, Guid ruleId, string? triggerRef, string remainingActionsJson, string contextJson, int depth, CancellationToken cancellationToken = default);
}
