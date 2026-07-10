using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to ops.workflow_rules (lexflow-database Scripts/07_Ops/WorkflowRules). Backs Settings §12 Workflow Rules.</summary>
public sealed class WorkflowRule : AuditableEntity
{
    private WorkflowRule()
    {
    }

    public WorkflowRule(Guid tenantId, string name, string triggerEvent, string conditionsJson, string actionsJson)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Name = name;
        TriggerEvent = triggerEvent;
        ConditionsJson = conditionsJson;
        ActionsJson = actionsJson;
        Active = true;
        RunOrder = 0;
    }

    public string Name { get; private set; } = null!;
    public string TriggerEvent { get; private set; } = null!;
    public string ConditionsJson { get; private set; } = "{}";
    public string ActionsJson { get; private set; } = "[]";
    public bool Active { get; private set; }
    public int RunOrder { get; private set; }

    public void Update(string name, string triggerEvent, string conditionsJson, string actionsJson, bool active, int runOrder)
    {
        Name = name;
        TriggerEvent = triggerEvent;
        ConditionsJson = conditionsJson;
        ActionsJson = actionsJson;
        Active = active;
        RunOrder = runOrder;
    }
}
