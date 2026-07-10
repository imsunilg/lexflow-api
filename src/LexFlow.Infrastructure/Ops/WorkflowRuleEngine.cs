using System.Text.Json;
using Hangfire;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Ops;

/// <summary>
/// §23 Workflow Rules (Automation Engine) — the "workflow engine worker" half of
/// "event -> outbox -> Service Bus -> workflow engine worker". A Hangfire recurring job
/// calls <see cref="DispatchPendingAsync"/> (mirroring DocumentIndexDispatchService in
/// Module 7, standing in for a real Service Bus/queue consumer); for each pending
/// ops.workflow_event_outbox row it loads the tenant's active rules for that event_type
/// (ordered by run_order) and runs each rule's condition group against the event payload,
/// executing matching actions and logging an ops.workflow_runs row per rule.
///
/// Loop guard (§23: "a rule's actions cannot re-trigger itself; chain depth &lt;= 5"):
/// actions never publish new ops.workflow_event_outbox rows themselves — only
/// IWorkflowEventPublisher (called from the originating domain service) does that — so a
/// rule's own actions can structurally never cause another dispatch cycle. The one
/// exception, "wait", resumes the same rule's remaining actions via a Hangfire delayed
/// job rather than a new outbox event; <see cref="ResumeAsync"/>'s depth parameter caps
/// that continuation chain at 5.
/// </summary>
public sealed class WorkflowRuleEngine(
    LexFlowDbContext db,
    ITaskService taskService,
    INotificationService notificationService,
    IHttpClientFactory httpClientFactory,
    IBackgroundJobClient backgroundJobClient) : IWorkflowActionResumer
{
    private const int MaxWaitDepth = 5;

    public async Task DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = await db.WorkflowEventOutbox
            .Where(o => o.Status == "Pending")
            .OrderBy(o => o.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var outboxRow in pending)
        {
            outboxRow.MarkDispatched();
            await db.SaveChangesAsync(cancellationToken);

            try
            {
                var rules = await db.WorkflowRules
                    .Where(r => r.TenantId == outboxRow.TenantId && r.TriggerEvent == outboxRow.EventType && r.Active)
                    .OrderBy(r => r.RunOrder)
                    .ToListAsync(cancellationToken);

                using var payloadDoc = JsonDocument.Parse(outboxRow.Payload);
                foreach (var rule in rules)
                {
                    await RunRuleAsync(outboxRow.TenantId, rule, payloadDoc.RootElement, outboxRow.Id.ToString(), depth: 0, cancellationToken);
                }

                outboxRow.MarkDone();
            }
            catch (Exception ex)
            {
                outboxRow.MarkFailed(ex.Message);
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>The builder UI's "test with sample record" step — runs conditions+actions against a caller-supplied payload without touching the outbox.</summary>
    public async Task<WorkflowTestResult> TestRuleAsync(Guid tenantId, WorkflowRule rule, string samplePayloadJson, CancellationToken cancellationToken = default)
    {
        using var payloadDoc = JsonDocument.Parse(samplePayloadJson);
        var matched = EvaluateConditions(JsonDocument.Parse(rule.ConditionsJson).RootElement, payloadDoc.RootElement);
        var resultJson = matched
            ? await ExecuteActionsAsync(tenantId, rule.Id, payloadDoc.RootElement, JsonDocument.Parse(rule.ActionsJson).RootElement, depth: 0, cancellationToken)
            : "{\"skipped\":\"conditions not matched\"}";

        return new WorkflowTestResult(matched, resultJson);
    }

    private async Task RunRuleAsync(Guid tenantId, WorkflowRule rule, JsonElement payload, string triggerRef, int depth, CancellationToken cancellationToken)
    {
        var run = new Domain.Entities.WorkflowRun(tenantId, rule.Id, triggerRef);
        await db.WorkflowRuns.AddAsync(run, cancellationToken);

        try
        {
            using var conditionsDoc = JsonDocument.Parse(rule.ConditionsJson);
            if (!EvaluateConditions(conditionsDoc.RootElement, payload))
            {
                run.MarkSucceeded("{\"matched\":false}");
                return;
            }

            using var actionsDoc = JsonDocument.Parse(rule.ActionsJson);
            var resultJson = await ExecuteActionsAsync(tenantId, rule.Id, payload, actionsDoc.RootElement, depth, cancellationToken);
            run.MarkSucceeded(resultJson);
        }
        catch (Exception ex)
        {
            run.MarkFailed(JsonSerializer.Serialize(new { error = ex.Message }));
        }
    }

    private static bool EvaluateConditions(JsonElement conditions, JsonElement payload)
    {
        if (conditions.ValueKind != JsonValueKind.Object || !conditions.EnumerateObject().Any())
        {
            return true;
        }

        if (conditions.TryGetProperty("and", out var andGroup))
        {
            return andGroup.EnumerateArray().All(c => EvaluateConditions(c, payload));
        }

        if (conditions.TryGetProperty("or", out var orGroup))
        {
            return orGroup.EnumerateArray().Any(c => EvaluateConditions(c, payload));
        }

        if (!conditions.TryGetProperty("field", out var fieldProp))
        {
            return true;
        }

        var field = fieldProp.GetString()!;
        var op = conditions.TryGetProperty("op", out var opProp) ? opProp.GetString() ?? "eq" : "eq";
        conditions.TryGetProperty("value", out var expected);

        if (!TryResolveField(payload, field, out var actual))
        {
            return false;
        }

        return op switch
        {
            "eq" => JsonElementEquals(actual, expected),
            "neq" => !JsonElementEquals(actual, expected),
            "in" => expected.ValueKind == JsonValueKind.Array && expected.EnumerateArray().Any(v => JsonElementEquals(actual, v)),
            "contains" => actual.ValueKind == JsonValueKind.String && expected.ValueKind == JsonValueKind.String && actual.GetString()!.Contains(expected.GetString()!, StringComparison.OrdinalIgnoreCase),
            "gt" => CompareNumeric(actual, expected) > 0,
            "lt" => CompareNumeric(actual, expected) < 0,
            "gte" => CompareNumeric(actual, expected) >= 0,
            "lte" => CompareNumeric(actual, expected) <= 0,
            _ => false,
        };
    }

    private static bool TryResolveField(JsonElement payload, string dotPath, out JsonElement value)
    {
        var current = payload;
        foreach (var segment in dotPath.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                value = default;
                return false;
            }
        }

        value = current;
        return true;
    }

    private static bool JsonElementEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind == JsonValueKind.String && b.ValueKind == JsonValueKind.String)
        {
            return string.Equals(a.GetString(), b.GetString(), StringComparison.OrdinalIgnoreCase);
        }

        if ((a.ValueKind is JsonValueKind.Number) && (b.ValueKind is JsonValueKind.Number))
        {
            return a.GetDecimal() == b.GetDecimal();
        }

        if (a.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return a.GetBoolean() == b.GetBoolean();
        }

        return a.GetRawText() == b.GetRawText();
    }

    private static int CompareNumeric(JsonElement a, JsonElement b)
        => a.ValueKind == JsonValueKind.Number && b.ValueKind == JsonValueKind.Number ? a.GetDecimal().CompareTo(b.GetDecimal()) : 0;

    /// <summary>Executes actions in order; a "wait" action schedules the remaining actions via Hangfire and stops inline execution.</summary>
    private async Task<string> ExecuteActionsAsync(Guid tenantId, Guid ruleId, JsonElement payload, JsonElement actions, int depth, CancellationToken cancellationToken)
    {
        if (actions.ValueKind != JsonValueKind.Array)
        {
            return "{\"actions\":[]}";
        }

        var results = new List<object>();
        var list = actions.EnumerateArray().ToList();

        for (var i = 0; i < list.Count; i++)
        {
            var action = list[i];
            var type = action.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;

            if (type == "wait")
            {
                if (depth >= MaxWaitDepth)
                {
                    results.Add(new { type, skipped = "max wait-continuation depth reached" });
                    break;
                }

                var minutes = action.TryGetProperty("minutes", out var minutesProp) ? minutesProp.GetInt32() : 0;
                var remaining = JsonSerializer.Serialize(list.Skip(i + 1));
                backgroundJobClient.Schedule<IWorkflowActionResumer>(
                    resumer => resumer.ResumeAsync(tenantId, ruleId, null, remaining, payload.GetRawText(), depth + 1, CancellationToken.None),
                    TimeSpan.FromMinutes(minutes));
                results.Add(new { type, scheduledInMinutes = minutes });
                break;
            }

            var actionResult = await ExecuteActionAsync(tenantId, payload, action, type, cancellationToken);
            results.Add(actionResult);
        }

        return JsonSerializer.Serialize(new { actions = results });
    }

    private async Task<object> ExecuteActionAsync(Guid tenantId, JsonElement payload, JsonElement action, string? type, CancellationToken cancellationToken)
    {
        switch (type)
        {
            case "notify":
            {
                var userId = ResolveUserId(payload, action);
                if (userId is null)
                {
                    return new { type, skipped = "no resolvable target user" };
                }

                var channels = action.TryGetProperty("channels", out var channelsProp)
                    ? channelsProp.EnumerateArray().Select(c => c.GetString()!).ToList()
                    : ["InApp"];
                var title = action.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "Notification" : "Notification";
                var body = action.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() : null;
                var mandatory = action.TryGetProperty("mandatory", out var mandatoryProp) && mandatoryProp.GetBoolean();
                var kind = action.TryGetProperty("kind", out var kindProp) ? kindProp.GetString() ?? "workflow" : "workflow";

                await notificationService.NotifyAsync(tenantId, userId.Value, new NotifyRequest(kind, title, body, null, channels, mandatory), cancellationToken);
                return new { type, notified = userId };
            }

            case "escalate":
            {
                var userId = ResolveUserId(payload, action, fieldName: "toField", explicitName: "toUserId");
                if (userId is null)
                {
                    return new { type, skipped = "no resolvable escalation target" };
                }

                var title = action.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "Escalation" : "Escalation";
                var body = action.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() : null;
                await notificationService.NotifyAsync(tenantId, userId.Value, new NotifyRequest("workflow.escalation", title, body, null, ["Email", "InApp", "Push"], Mandatory: false), cancellationToken);
                return new { type, escalatedTo = userId };
            }

            case "create_task":
            {
                var title = action.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "Follow-up" : "Follow-up";
                var description = action.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
                var dueInDays = action.TryGetProperty("dueInDays", out var dueProp) ? dueProp.GetInt32() : 0;
                var category = action.TryGetProperty("category", out var categoryProp) ? categoryProp.GetString() : null;
                Guid? matterId = TryResolveField(payload, "matterId", out var matterField) && matterField.ValueKind == JsonValueKind.String && Guid.TryParse(matterField.GetString(), out var mId) ? mId : null;
                Guid? ownerId = ResolveUserId(payload, action, fieldName: "ownerField", explicitName: "ownerId");

                var created = await taskService.CreateAsync(tenantId, actorId: null, new CreateOpsTaskInput(title, description, matterId, ClientId: null, ownerId, DateTimeOffset.UtcNow.AddDays(dueInDays), "Medium", category), cancellationToken);
                return new { type, taskId = created.Id };
            }

            case "update_field":
            {
                if (!TryResolveField(payload, action.TryGetProperty("idField", out var idFieldProp) ? idFieldProp.GetString() ?? "entityId" : "entityId", out var idElement)
                    || idElement.ValueKind != JsonValueKind.String || !Guid.TryParse(idElement.GetString(), out var entityId))
                {
                    return new { type, skipped = "no resolvable entity id" };
                }

                var field = action.TryGetProperty("field", out var fieldProp) ? fieldProp.GetString() : null;
                var value = action.TryGetProperty("value", out var valueProp) ? valueProp.GetString() : null;
                if (field == "status" && value is not null)
                {
                    await taskService.SetStatusAsync(tenantId, actorId: null, entityId, value, cancellationToken);
                }

                return new { type, entityId, field, value };
            }

            case "assign":
            {
                var pool = action.TryGetProperty("pool", out var poolProp)
                    ? poolProp.EnumerateArray().Select(p => Guid.Parse(p.GetString()!)).ToList()
                    : [];
                if (pool.Count == 0)
                {
                    return new { type, skipped = "empty assignment pool" };
                }

                var counts = new Dictionary<Guid, int>();
                foreach (var candidate in pool)
                {
                    counts[candidate] = await db.OpsTasks.CountAsync(t => t.TenantId == tenantId && t.OwnerId == candidate && t.Status != "Done" && t.Status != "Cancelled", cancellationToken);
                }

                var chosen = counts.OrderBy(kv => kv.Value).First().Key;
                return new { type, assigned = chosen };
            }

            case "webhook":
            {
                var url = action.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
                if (string.IsNullOrWhiteSpace(url))
                {
                    return new { type, skipped = "no url configured" };
                }

                using var client = httpClientFactory.CreateClient();
                using var response = await client.PostAsync(url, new StringContent(payload.GetRawText(), System.Text.Encoding.UTF8, "application/json"), cancellationToken);
                return new { type, statusCode = (int)response.StatusCode };
            }

            default:
                return new { type = type ?? "unknown", skipped = "unrecognized action type" };
        }
    }

    private static Guid? ResolveUserId(JsonElement payload, JsonElement action, string fieldName = "targetField", string explicitName = "userId")
    {
        if (action.TryGetProperty(explicitName, out var explicitProp) && explicitProp.ValueKind == JsonValueKind.String && Guid.TryParse(explicitProp.GetString(), out var explicitId))
        {
            return explicitId;
        }

        if (action.TryGetProperty(fieldName, out var fieldProp) && fieldProp.ValueKind == JsonValueKind.String)
        {
            var path = fieldProp.GetString()!;
            if (TryResolveField(payload, path, out var resolved) && resolved.ValueKind == JsonValueKind.String && Guid.TryParse(resolved.GetString(), out var resolvedId))
            {
                return resolvedId;
            }
        }

        return null;
    }

    public async Task ResumeAsync(Guid tenantId, Guid ruleId, string? triggerRef, string remainingActionsJson, string contextJson, int depth, CancellationToken cancellationToken = default)
    {
        var rule = await db.WorkflowRules.SingleOrDefaultAsync(r => r.TenantId == tenantId && r.Id == ruleId, cancellationToken);
        if (rule is null || !rule.Active)
        {
            return;
        }

        using var payloadDoc = JsonDocument.Parse(contextJson);
        using var actionsDoc = JsonDocument.Parse(remainingActionsJson);

        var run = new Domain.Entities.WorkflowRun(tenantId, ruleId, triggerRef);
        await db.WorkflowRuns.AddAsync(run, cancellationToken);

        try
        {
            var resultJson = await ExecuteActionsAsync(tenantId, ruleId, payloadDoc.RootElement, actionsDoc.RootElement, depth, cancellationToken);
            run.MarkSucceeded(resultJson);
        }
        catch (Exception ex)
        {
            run.MarkFailed(JsonSerializer.Serialize(new { error = ex.Message }));
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
