using FluentAssertions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Crm;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Crm;

/// <summary>Module 2 duplicate-guard, stage-transition rules (AC-L2), and the AC-L3 atomic-convert transaction, against EF InMemory.</summary>
public sealed class LeadServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private static CreateLeadInput Input(string? phone = "+919876543210", string? email = "lead@example.com") =>
        new("Asha", "Rao", null, email, phone, null, null, null, null, "Contract dispute", null, null);

    [Fact]
    public async Task CreateAsync_rejects_a_second_open_lead_with_the_same_phone()
    {
        await using var db = CreateContext(nameof(CreateAsync_rejects_a_second_open_lead_with_the_same_phone));
        var service = new LeadService(db);
        var tenantId = Guid.NewGuid();

        await service.CreateAsync(tenantId, null, Input(), CancellationToken.None);

        var act = () => service.CreateAsync(tenantId, null, Input(email: "other@example.com"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().Where(e => e.Code == "LEAD_DUPLICATE_PHONE");
    }

    [Fact]
    public async Task ChangeStageAsync_blocks_a_non_adjacent_jump_without_the_skip_permission()
    {
        await using var db = CreateContext(nameof(ChangeStageAsync_blocks_a_non_adjacent_jump_without_the_skip_permission));
        var service = new LeadService(db);
        var tenantId = Guid.NewGuid();
        var lead = await service.CreateAsync(tenantId, null, Input(), CancellationToken.None);

        var act = () => service.ChangeStageAsync(tenantId, null, lead.Id, "Proposal Sent", null, allowSkip: false, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().Where(e => e.Code == "STAGE_SKIP_NOT_ALLOWED");
    }

    [Fact]
    public async Task ChangeStageAsync_allows_the_same_jump_when_the_skip_permission_is_granted()
    {
        await using var db = CreateContext(nameof(ChangeStageAsync_allows_the_same_jump_when_the_skip_permission_is_granted));
        var service = new LeadService(db);
        var tenantId = Guid.NewGuid();
        var lead = await service.CreateAsync(tenantId, null, Input(), CancellationToken.None);

        var result = await service.ChangeStageAsync(tenantId, null, lead.Id, "Proposal Sent", null, allowSkip: true, CancellationToken.None);

        result.Stage.Should().Be("Proposal Sent");
        var history = await db.LeadStageHistory.Where(h => h.LeadId == lead.Id).ToListAsync();
        history.Should().ContainSingle(h => h.ToStage == "Proposal Sent");
    }

    [Fact]
    public async Task ConvertAsync_rejects_createMatter_since_the_Legal_module_does_not_exist_yet()
    {
        await using var db = CreateContext(nameof(ConvertAsync_rejects_createMatter_since_the_Legal_module_does_not_exist_yet));
        var service = new LeadService(db);
        var tenantId = Guid.NewGuid();
        var lead = await service.CreateAsync(tenantId, null, Input(), CancellationToken.None);
        await service.ChangeStageAsync(tenantId, null, lead.Id, "Consultation Done", null, true, CancellationToken.None);

        var act = () => service.ConvertAsync(tenantId, null, lead.Id, createMatter: true, null, null, force: false, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().Where(e => e.Code == "MODULE_NOT_AVAILABLE");
    }

    [Fact]
    public async Task ConvertAsync_blocks_conversion_before_Consultation_Done_unless_forced()
    {
        await using var db = CreateContext(nameof(ConvertAsync_blocks_conversion_before_Consultation_Done_unless_forced));
        var service = new LeadService(db);
        var tenantId = Guid.NewGuid();
        var lead = await service.CreateAsync(tenantId, null, Input(), CancellationToken.None);

        var act = () => service.ConvertAsync(tenantId, null, lead.Id, createMatter: false, null, null, force: false, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().Where(e => e.Code == "CONVERT_STAGE_TOO_EARLY");
    }

    [Fact]
    public async Task ConvertAsync_allows_conversion_before_Consultation_Done_when_forced()
    {
        await using var db = CreateContext(nameof(ConvertAsync_allows_conversion_before_Consultation_Done_when_forced));
        var service = new LeadService(db);
        var tenantId = Guid.NewGuid();
        var lead = await service.CreateAsync(tenantId, null, Input(), CancellationToken.None);

        var result = await service.ConvertAsync(tenantId, null, lead.Id, createMatter: false, null, null, force: true, CancellationToken.None);

        result.ClientId.Should().NotBeEmpty();
        var reloadedLead = await db.Leads.SingleAsync(l => l.Id == lead.Id);
        reloadedLead.Status.Should().Be("Converted");
    }

    [Fact]
    public async Task ConvertAsync_creates_the_client_and_marks_the_lead_converted_atomically()
    {
        await using var db = CreateContext(nameof(ConvertAsync_creates_the_client_and_marks_the_lead_converted_atomically));
        var service = new LeadService(db);
        var tenantId = Guid.NewGuid();
        var lead = await service.CreateAsync(tenantId, null, Input(), CancellationToken.None);
        await service.ChangeStageAsync(tenantId, null, lead.Id, "Consultation Done", null, true, CancellationToken.None);

        var result = await service.ConvertAsync(tenantId, null, lead.Id, createMatter: false, null, null, force: false, CancellationToken.None);

        result.ClientId.Should().NotBeEmpty();
        result.MatterId.Should().BeNull();

        var reloadedLead = await db.Leads.SingleAsync(l => l.Id == lead.Id);
        reloadedLead.Status.Should().Be("Converted");
        reloadedLead.ConvertedClientId.Should().Be(result.ClientId);

        var client = await db.Clients.SingleAsync(c => c.Id == result.ClientId);
        client.SourceLeadId.Should().Be(lead.Id);
        client.PhoneE164.Should().Be(lead.PhoneE164);
    }

    [Fact]
    public async Task MarkLostAsync_requires_a_reason_that_exists_for_the_tenant()
    {
        await using var db = CreateContext(nameof(MarkLostAsync_requires_a_reason_that_exists_for_the_tenant));
        var service = new LeadService(db);
        var tenantId = Guid.NewGuid();
        var lead = await service.CreateAsync(tenantId, null, Input(), CancellationToken.None);

        var act = () => service.MarkLostAsync(tenantId, null, lead.Id, Guid.NewGuid(), "Went with a competitor", CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task AssignAsync_rejects_a_rule_only_assignment_since_no_rule_engine_is_modeled()
    {
        await using var db = CreateContext(nameof(AssignAsync_rejects_a_rule_only_assignment_since_no_rule_engine_is_modeled));
        var service = new LeadService(db);
        var tenantId = Guid.NewGuid();
        var lead = await service.CreateAsync(tenantId, null, Input(), CancellationToken.None);

        var act = () => service.AssignAsync(tenantId, lead.Id, userId: null, ruleId: Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().Where(e => e.Code == "ASSIGNMENT_RULE_NOT_SUPPORTED");
    }
}
