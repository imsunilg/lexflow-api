using System.Reflection;
using System.Text.Json;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Ops;

/// <summary>§23 Workflow Rules (Automation Engine) — CRUD, run log, builder-UI "test with sample record", and the shipped-defaults seeder.</summary>
public sealed class WorkflowRuleService(LexFlowDbContext db, WorkflowRuleEngine engine) : IWorkflowRuleService
{
    private const string DefaultRulesResourceName = "LexFlow.Infrastructure.Ops.Data.DefaultWorkflowRules.json";

    public async Task<WorkflowRuleDto> CreateAsync(Guid tenantId, string name, string triggerEvent, string conditionsJson, string actionsJson, int runOrder, CancellationToken cancellationToken = default)
    {
        var rule = new WorkflowRule(tenantId, name, triggerEvent, conditionsJson, actionsJson);
        rule.Update(name, triggerEvent, conditionsJson, actionsJson, active: true, runOrder);
        await db.WorkflowRules.AddAsync(rule, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(rule);
    }

    public async Task<WorkflowRuleDto?> GetByIdAsync(Guid tenantId, Guid ruleId, CancellationToken cancellationToken = default)
    {
        var rule = await db.WorkflowRules.SingleOrDefaultAsync(r => r.TenantId == tenantId && r.Id == ruleId, cancellationToken);
        return rule is null ? null : ToDto(rule);
    }

    public async Task<IReadOnlyList<WorkflowRuleDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var rules = await db.WorkflowRules.Where(r => r.TenantId == tenantId).OrderBy(r => r.RunOrder).ToListAsync(cancellationToken);
        return rules.Select(ToDto).ToList();
    }

    public async Task<WorkflowRuleDto> UpdateAsync(Guid tenantId, Guid ruleId, string name, string triggerEvent, string conditionsJson, string actionsJson, bool active, int runOrder, CancellationToken cancellationToken = default)
    {
        var rule = await db.WorkflowRules.SingleOrDefaultAsync(r => r.TenantId == tenantId && r.Id == ruleId, cancellationToken)
            ?? throw new NotFoundException(nameof(WorkflowRule), ruleId);
        rule.Update(name, triggerEvent, conditionsJson, actionsJson, active, runOrder);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(rule);
    }

    public async Task DeleteAsync(Guid tenantId, Guid ruleId, CancellationToken cancellationToken = default)
    {
        var rule = await db.WorkflowRules.SingleOrDefaultAsync(r => r.TenantId == tenantId && r.Id == ruleId, cancellationToken);
        if (rule is not null)
        {
            db.WorkflowRules.Remove(rule);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<WorkflowTestResult> TestAsync(Guid tenantId, Guid ruleId, string samplePayloadJson, CancellationToken cancellationToken = default)
    {
        var rule = await db.WorkflowRules.SingleOrDefaultAsync(r => r.TenantId == tenantId && r.Id == ruleId, cancellationToken)
            ?? throw new NotFoundException(nameof(WorkflowRule), ruleId);
        return await engine.TestRuleAsync(tenantId, rule, samplePayloadJson, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowRunDto>> GetRunsAsync(Guid tenantId, Guid ruleId, CancellationToken cancellationToken = default)
    {
        var runs = await db.WorkflowRuns.Where(r => r.TenantId == tenantId && r.RuleId == ruleId).OrderByDescending(r => r.CreatedAt).ToListAsync(cancellationToken);
        return runs.Select(r => new WorkflowRunDto(r.Id, r.RuleId, r.TriggerRef, r.Status, r.ExecutedAt, r.ResultJson)).ToList();
    }

    /// <summary>
    /// Application-level seeding (explicitly not raw SQL, per the build request): reads the
    /// 12 shipped default rules from an embedded JSON resource and inserts any whose name
    /// doesn't already exist for this tenant — idempotent, so calling it again (or on every
    /// tenant-onboarding run) is always safe.
    /// </summary>
    public async Task<int> SeedDefaultRulesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var existingNames = await db.WorkflowRules.Where(r => r.TenantId == tenantId).Select(r => r.Name).ToListAsync(cancellationToken);
        var defaults = LoadDefaultRuleDefinitions();

        var inserted = 0;
        foreach (var definition in defaults)
        {
            if (existingNames.Contains(definition.Name))
            {
                continue;
            }

            var rule = new WorkflowRule(tenantId, definition.Name, definition.TriggerEvent, definition.ConditionsJson, definition.ActionsJson);
            rule.Update(definition.Name, definition.TriggerEvent, definition.ConditionsJson, definition.ActionsJson, active: true, definition.RunOrder);
            await db.WorkflowRules.AddAsync(rule, cancellationToken);
            inserted++;
        }

        if (inserted > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return inserted;
    }

    private static IReadOnlyList<DefaultRuleDefinition> LoadDefaultRuleDefinitions()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(DefaultRulesResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{DefaultRulesResourceName}' not found.");
        using var doc = JsonDocument.Parse(stream);

        var definitions = new List<DefaultRuleDefinition>();
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            definitions.Add(new DefaultRuleDefinition(
                element.GetProperty("name").GetString()!,
                element.GetProperty("triggerEvent").GetString()!,
                element.GetProperty("conditions").GetRawText(),
                element.GetProperty("actions").GetRawText(),
                element.GetProperty("runOrder").GetInt32()));
        }

        return definitions;
    }

    private static WorkflowRuleDto ToDto(WorkflowRule r) => new(r.Id, r.Name, r.TriggerEvent, r.ConditionsJson, r.ActionsJson, r.Active, r.RunOrder);

    private sealed record DefaultRuleDefinition(string Name, string TriggerEvent, string ConditionsJson, string ActionsJson, int RunOrder);
}
