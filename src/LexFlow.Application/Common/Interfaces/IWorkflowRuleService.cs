namespace LexFlow.Application.Common.Interfaces;

/// <summary>§23 Workflow Rules (Automation Engine) — CRUD over ops.workflow_rules plus the run log.</summary>
public interface IWorkflowRuleService
{
    Task<WorkflowRuleDto> CreateAsync(Guid tenantId, string name, string triggerEvent, string conditionsJson, string actionsJson, int runOrder, CancellationToken cancellationToken = default);

    Task<WorkflowRuleDto?> GetByIdAsync(Guid tenantId, Guid ruleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowRuleDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<WorkflowRuleDto> UpdateAsync(Guid tenantId, Guid ruleId, string name, string triggerEvent, string conditionsJson, string actionsJson, bool active, int runOrder, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid tenantId, Guid ruleId, CancellationToken cancellationToken = default);

    /// <summary>Runs the rule's condition+action pipeline against a caller-supplied sample payload without touching ops.workflow_event_outbox — the builder UI's "test with sample record" step.</summary>
    Task<WorkflowTestResult> TestAsync(Guid tenantId, Guid ruleId, string samplePayloadJson, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowRunDto>> GetRunsAsync(Guid tenantId, Guid ruleId, CancellationToken cancellationToken = default);

    /// <summary>Idempotent, application-level (not raw SQL) — loads the 12 shipped default rules from an embedded JSON resource and inserts any whose name doesn't already exist for this tenant.</summary>
    Task<int> SeedDefaultRulesAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed record WorkflowRuleDto(Guid Id, string Name, string TriggerEvent, string ConditionsJson, string ActionsJson, bool Active, int RunOrder);

public sealed record WorkflowRunDto(Guid Id, Guid RuleId, string? TriggerRef, string Status, DateTimeOffset? ExecutedAt, string ResultJson);

public sealed record WorkflowTestResult(bool ConditionsMatched, string ResultJson);
