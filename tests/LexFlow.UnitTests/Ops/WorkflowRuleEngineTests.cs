using System.Text.Json;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Ops;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Ops;

/// <summary>
/// §23 Workflow Rules (Automation Engine): the outbox dispatcher, condition evaluator,
/// and a representative slice of actions (notify, create_task, assign). Loop-guard by
/// design: actions never publish new outbox rows themselves (see WorkflowRuleEngine's own
/// doc comment), so there's nothing to assert there beyond "no such call exists."
/// </summary>
public sealed class WorkflowRuleEngineTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private static WorkflowRuleEngine CreateEngine(LexFlowDbContext db)
        => new(db, new TaskService(db), new NotificationService(db, new NoopEmail(), new NoopSms(), new NoopWhatsApp(), new NoopPush()), new FakeHttpClientFactory(), new FakeBackgroundJobClient());

    [Fact]
    public async Task DispatchPendingAsync_runs_a_matching_rule_and_marks_the_outbox_row_Done()
    {
        await using var db = CreateContext(nameof(DispatchPendingAsync_runs_a_matching_rule_and_marks_the_outbox_row_Done));
        var tenantId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        await db.Users.AddAsync(new User(tenantId, "owner@example.com", "Owner"));

        var actions = JsonSerializer.Serialize(new object[] { new { type = "notify", channels = new[] { "InApp" }, targetField = "ownerId", title = "New lead" } });
        var rule = new WorkflowRule(tenantId, "Lead assigned notify", "lead.created", "{}", actions);
        await db.WorkflowRules.AddAsync(rule);

        var payload = JsonSerializer.Serialize(new { entityId = Guid.NewGuid(), ownerId });
        var outbox = new WorkflowEventOutbox(tenantId, "lead.created", null, payload);
        await db.WorkflowEventOutbox.AddAsync(outbox);
        await db.SaveChangesAsync();

        var engine = CreateEngine(db);
        await engine.DispatchPendingAsync(CancellationToken.None);

        var reloadedOutbox = await db.WorkflowEventOutbox.SingleAsync(o => o.Id == outbox.Id);
        reloadedOutbox.Status.Should().Be("Done");

        var run = await db.WorkflowRuns.SingleAsync(r => r.RuleId == rule.Id);
        run.Status.Should().Be("Succeeded");

        (await db.Notifications.CountAsync(n => n.UserId == ownerId)).Should().Be(1);
    }

    [Fact]
    public async Task DispatchPendingAsync_only_runs_rules_whose_conditions_match_the_payload()
    {
        await using var db = CreateContext(nameof(DispatchPendingAsync_only_runs_rules_whose_conditions_match_the_payload));
        var tenantId = Guid.NewGuid();

        var conditions = JsonSerializer.Serialize(new { field = "sineDie", op = "eq", value = true });
        var actions = JsonSerializer.Serialize(new object[] { new { type = "webhook", url = "https://example.test/hook" } });
        var rule = new WorkflowRule(tenantId, "Sine die review", "hearing.outcome_recorded", conditions, actions);
        await db.WorkflowRules.AddAsync(rule);

        var nonMatchingPayload = JsonSerializer.Serialize(new { sineDie = false });
        await db.WorkflowEventOutbox.AddAsync(new WorkflowEventOutbox(tenantId, "hearing.outcome_recorded", null, nonMatchingPayload));
        await db.SaveChangesAsync();

        var engine = CreateEngine(db);
        await engine.DispatchPendingAsync(CancellationToken.None);

        var run = await db.WorkflowRuns.SingleAsync(r => r.RuleId == rule.Id);
        run.ResultJson.Should().Contain("\"matched\":false");
    }

    [Fact]
    public async Task DispatchPendingAsync_create_task_action_creates_an_OpsTask()
    {
        await using var db = CreateContext(nameof(DispatchPendingAsync_create_task_action_creates_an_OpsTask));
        var tenantId = Guid.NewGuid();

        var actions = JsonSerializer.Serialize(new object[] { new { type = "create_task", title = "Review document", category = "Admin", dueInDays = 2 } });
        var rule = new WorkflowRule(tenantId, "Document review task", "document.uploaded", "{}", actions);
        await db.WorkflowRules.AddAsync(rule);
        await db.WorkflowEventOutbox.AddAsync(new WorkflowEventOutbox(tenantId, "document.uploaded", null, "{}"));
        await db.SaveChangesAsync();

        var engine = CreateEngine(db);
        await engine.DispatchPendingAsync(CancellationToken.None);

        (await db.OpsTasks.CountAsync(t => t.TenantId == tenantId && t.Title == "Review document")).Should().Be(1);
    }

    [Fact]
    public async Task DispatchPendingAsync_assign_action_picks_the_least_loaded_candidate_from_the_pool()
    {
        await using var db = CreateContext(nameof(DispatchPendingAsync_assign_action_picks_the_least_loaded_candidate_from_the_pool));
        var tenantId = Guid.NewGuid();
        var busyUser = Guid.NewGuid();
        var idleUser = Guid.NewGuid();

        var taskService = new TaskService(db);
        await taskService.CreateAsync(tenantId, null, new CreateOpsTaskInput("Existing", null, null, null, busyUser, DateTimeOffset.UtcNow.AddDays(1), "Medium", null), CancellationToken.None);

        var actions = JsonSerializer.Serialize(new object[] { new { type = "assign", pool = new[] { busyUser.ToString(), idleUser.ToString() } } });
        var rule = new WorkflowRule(tenantId, "Round robin", "lead.created", "{}", actions);
        await db.WorkflowRules.AddAsync(rule);
        await db.WorkflowEventOutbox.AddAsync(new WorkflowEventOutbox(tenantId, "lead.created", null, "{}"));
        await db.SaveChangesAsync();

        var engine = new WorkflowRuleEngine(db, taskService, new NotificationService(db, new NoopEmail(), new NoopSms(), new NoopWhatsApp(), new NoopPush()), new FakeHttpClientFactory(), new FakeBackgroundJobClient());
        await engine.DispatchPendingAsync(CancellationToken.None);

        var run = await db.WorkflowRuns.SingleAsync(r => r.RuleId == rule.Id);
        run.ResultJson.Should().Contain(idleUser.ToString());
    }

    [Fact]
    public async Task TestRuleAsync_evaluates_against_a_sample_payload_without_touching_the_outbox()
    {
        await using var db = CreateContext(nameof(TestRuleAsync_evaluates_against_a_sample_payload_without_touching_the_outbox));
        var tenantId = Guid.NewGuid();
        var rule = new WorkflowRule(tenantId, "Sample test", "invoice.submitted", "{\"field\":\"amount\",\"op\":\"gt\",\"value\":100000}", "[]");

        var engine = CreateEngine(db);
        var result = await engine.TestRuleAsync(tenantId, rule, "{\"amount\":150000}", CancellationToken.None);

        result.ConditionsMatched.Should().BeTrue();
        (await db.WorkflowEventOutbox.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SeedDefaultRulesAsync_inserts_the_12_shipped_rules_and_is_idempotent()
    {
        await using var db = CreateContext(nameof(SeedDefaultRulesAsync_inserts_the_12_shipped_rules_and_is_idempotent));
        var tenantId = Guid.NewGuid();
        var engine = CreateEngine(db);
        var service = new WorkflowRuleService(db, engine);

        var firstRun = await service.SeedDefaultRulesAsync(tenantId, CancellationToken.None);
        var secondRun = await service.SeedDefaultRulesAsync(tenantId, CancellationToken.None);

        firstRun.Should().Be(12);
        secondRun.Should().Be(0, "seeding is idempotent — a rule whose name already exists for this tenant is skipped");
        (await db.WorkflowRules.CountAsync(r => r.TenantId == tenantId)).Should().Be(12);
    }

    private sealed class NoopEmail : IEmailNotificationProvider
    {
        public Task<bool> SendAsync(string toEmail, string title, string? body, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class NoopSms : ISmsNotificationProvider
    {
        public Task<bool> SendAsync(string toPhone, string title, string? body, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class NoopWhatsApp : IWhatsAppNotificationProvider
    {
        public Task<bool> SendAsync(string toPhone, string title, string? body, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class NoopPush : IPushNotificationProvider
    {
        public Task<bool> SendAsync(Guid userId, string title, string? body, string? deepLink, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class FakeBackgroundJobClient : IBackgroundJobClient
    {
        public string Create(Job job, IState state) => Guid.NewGuid().ToString();

        public bool ChangeState(string jobId, IState state, string? expectedState) => true;
    }
}
