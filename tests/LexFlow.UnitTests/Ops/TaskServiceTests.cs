using FluentAssertions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Ops;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Ops;

/// <summary>Module 10: status-workflow gating (AC-TK2), templates (AC-TK3), smart parse (AC-TK1), and the CYCLE_DETECTED translation helper.</summary>
public sealed class TaskServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private static CreateOpsTaskInput Input(string title = "File written statement", Guid? matterId = null, Guid? ownerId = null) =>
        new(title, null, matterId, null, ownerId, DateTimeOffset.UtcNow.AddDays(3), "Medium", "Filing");

    [Fact]
    public async Task SetStatusAsync_blocks_moving_to_InProgress_while_a_dependency_is_open()
    {
        await using var db = CreateContext(nameof(SetStatusAsync_blocks_moving_to_InProgress_while_a_dependency_is_open));
        var service = new TaskService(db);
        var tenantId = Guid.NewGuid();

        var predecessor = await service.CreateAsync(tenantId, null, Input("Predecessor"), CancellationToken.None);
        var dependent = await service.CreateAsync(tenantId, null, Input("Dependent"), CancellationToken.None);
        await service.AddDependencyAsync(tenantId, null, dependent.Id, predecessor.Id, CancellationToken.None);

        var act = () => service.SetStatusAsync(tenantId, null, dependent.Id, "InProgress", CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().Where(e => e.Code == "TASK_BLOCKED");
    }

    [Fact]
    public async Task SetStatusAsync_allows_InProgress_once_the_predecessor_is_Done()
    {
        await using var db = CreateContext(nameof(SetStatusAsync_allows_InProgress_once_the_predecessor_is_Done));
        var service = new TaskService(db);
        var tenantId = Guid.NewGuid();

        var predecessor = await service.CreateAsync(tenantId, null, Input("Predecessor"), CancellationToken.None);
        var dependent = await service.CreateAsync(tenantId, null, Input("Dependent"), CancellationToken.None);
        await service.AddDependencyAsync(tenantId, null, dependent.Id, predecessor.Id, CancellationToken.None);
        await service.SetStatusAsync(tenantId, null, predecessor.Id, "Done", CancellationToken.None);

        var result = await service.SetStatusAsync(tenantId, null, dependent.Id, "InProgress", CancellationToken.None);

        result.Status.Should().Be("InProgress");
    }

    [Fact]
    public async Task SetStatusAsync_blocks_Done_while_a_mandatory_checklist_item_is_unticked()
    {
        await using var db = CreateContext(nameof(SetStatusAsync_blocks_Done_while_a_mandatory_checklist_item_is_unticked));
        var service = new TaskService(db);
        var tenantId = Guid.NewGuid();

        var task = await service.CreateAsync(tenantId, null, Input(), CancellationToken.None);
        await service.AddChecklistItemAsync(tenantId, task.Id, "File the affidavit", isMandatory: true, sortOrder: 0, CancellationToken.None);

        var act = () => service.SetStatusAsync(tenantId, null, task.Id, "Done", CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().Where(e => e.Code == "CHECKLIST_INCOMPLETE");
    }

    [Fact]
    public async Task SetStatusAsync_allows_Done_once_the_mandatory_checklist_item_is_ticked()
    {
        await using var db = CreateContext(nameof(SetStatusAsync_allows_Done_once_the_mandatory_checklist_item_is_ticked));
        var service = new TaskService(db);
        var tenantId = Guid.NewGuid();

        var task = await service.CreateAsync(tenantId, null, Input(), CancellationToken.None);
        var item = await service.AddChecklistItemAsync(tenantId, task.Id, "File the affidavit", isMandatory: true, sortOrder: 0, CancellationToken.None);
        await service.SetChecklistItemDoneAsync(tenantId, task.Id, item.Id, true, CancellationToken.None);

        var result = await service.SetStatusAsync(tenantId, null, task.Id, "Done", CancellationToken.None);

        result.Status.Should().Be("Done");
        result.ProgressPct.Should().Be(100);
    }

    [Fact]
    public void IsCycleDetectedError_recognizes_the_trigger_raised_exception_and_ignores_unrelated_failures()
    {
        var cycleException = new DbUpdateException("insert failed", new InvalidOperationException("23514: CYCLE_DETECTED: adding dependency a -> b would create a cycle"));
        var unrelatedException = new DbUpdateException("insert failed", new InvalidOperationException("duplicate key value violates unique constraint"));

        TaskService.IsCycleDetectedError(cycleException).Should().BeTrue();
        TaskService.IsCycleDetectedError(unrelatedException).Should().BeFalse();
    }

    [Fact]
    public async Task AddDependencyAsync_rejects_a_task_depending_on_itself_without_touching_the_database()
    {
        await using var db = CreateContext(nameof(AddDependencyAsync_rejects_a_task_depending_on_itself_without_touching_the_database));
        var service = new TaskService(db);
        var tenantId = Guid.NewGuid();
        var task = await service.CreateAsync(tenantId, null, Input(), CancellationToken.None);

        var act = () => service.AddDependencyAsync(tenantId, null, task.Id, task.Id, CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "CYCLE_DETECTED");
    }

    [Fact]
    public async Task ApplyTemplateToMatterAsync_is_idempotent_per_matter_and_template()
    {
        await using var db = CreateContext(nameof(ApplyTemplateToMatterAsync_is_idempotent_per_matter_and_template));
        var service = new TaskService(db);
        var tenantId = Guid.NewGuid();
        var matterId = Guid.NewGuid();

        var template = await service.CreateTemplateAsync(tenantId, "New Civil Suit", "Litigation",
            [new TaskTemplateItemInput("File written statement", 14, "Filing", 0, true), new TaskTemplateItemInput("Serve notice", 3, "Filing", 1, true)],
            CancellationToken.None);

        var openedOn = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstApply = await service.ApplyTemplateToMatterAsync(tenantId, null, matterId, template.Id, openedOn, CancellationToken.None);
        var secondApply = await service.ApplyTemplateToMatterAsync(tenantId, null, matterId, template.Id, openedOn, CancellationToken.None);

        firstApply.Should().HaveCount(2);
        secondApply.Should().BeEmpty("re-applying the same template to the same matter must skip already-created items");

        var allTasksForMatter = await service.GetAllAsync(tenantId, new OpsTaskFilter(null, null, null, matterId, null, null, null, null), CancellationToken.None);
        allTasksForMatter.Should().HaveCount(2);
    }

    [Fact]
    public async Task ApplyTemplateToMatterAsync_computes_due_dates_relative_to_the_supplied_date()
    {
        await using var db = CreateContext(nameof(ApplyTemplateToMatterAsync_computes_due_dates_relative_to_the_supplied_date));
        var service = new TaskService(db);
        var tenantId = Guid.NewGuid();
        var matterId = Guid.NewGuid();

        var template = await service.CreateTemplateAsync(tenantId, "New Civil Suit", "Litigation",
            [new TaskTemplateItemInput("File written statement", 14, "Filing", 0, true)],
            CancellationToken.None);

        var openedOn = new DateOnly(2026, 1, 1);
        var created = await service.ApplyTemplateToMatterAsync(tenantId, null, matterId, template.Id, openedOn, CancellationToken.None);

        created.Single().DueAt!.Value.Date.Should().Be(new DateTime(2026, 1, 15));
    }

    [Fact]
    public async Task ParseAsync_extracts_matter_assignee_due_date_and_priority_from_the_PRDs_own_example()
    {
        await using var db = CreateContext(nameof(ParseAsync_extracts_matter_assignee_due_date_and_priority_from_the_PRDs_own_example));
        var service = new TaskService(db);
        var tenantId = Guid.NewGuid();

        var client = new Client(tenantId, "CL-1", "Individual", "Priya", "Shah", null, null, null, null, null, null, null, null);
        await db.Clients.AddAsync(client);
        var matter = new Matter(tenantId, "MAT-042", "Test matter", client.Id, "Litigation", null, null, null, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        await db.Matters.AddAsync(matter);
        var aditi = new User(tenantId, "aditi@example.com", "Aditi Sharma");
        await db.Users.AddAsync(aditi);
        await db.SaveChangesAsync();

        var draft = await service.ParseAsync(tenantId, "File written statement for MAT-042 by next Friday @Aditi !high", CancellationToken.None);

        draft.MatterNumber.Should().Be("MAT-042");
        draft.MatterId.Should().Be(matter.Id);
        draft.AssigneeUserId.Should().Be(aditi.Id);
        draft.Priority.Should().Be("High");
        draft.DueAt.Should().NotBeNull();
        draft.DueAt!.Value.DayOfWeek.Should().Be(DayOfWeek.Friday);
    }

    [Fact]
    public async Task GetWorkloadAsync_counts_match_a_filtered_GetAllAsync_call_exactly()
    {
        await using var db = CreateContext(nameof(GetWorkloadAsync_counts_match_a_filtered_GetAllAsync_call_exactly));
        var service = new TaskService(db);
        var tenantId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow);

        await service.CreateAsync(tenantId, null, new CreateOpsTaskInput("Task 1", null, null, null, ownerId, new DateTimeOffset(weekStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddHours(1), "Medium", null), CancellationToken.None);
        await service.CreateAsync(tenantId, null, new CreateOpsTaskInput("Task 2", null, null, null, ownerId, new DateTimeOffset(weekStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(2), "Medium", null), CancellationToken.None);

        var workload = await service.GetWorkloadAsync(tenantId, null, weekStart, CancellationToken.None);
        var filtered = await service.GetAllAsync(tenantId, new OpsTaskFilter(ownerId, null, null, null, null, null, null, null), CancellationToken.None);

        workload.Single(w => w.UserId == ownerId).Total.Should().Be(filtered.Count);
    }
}
