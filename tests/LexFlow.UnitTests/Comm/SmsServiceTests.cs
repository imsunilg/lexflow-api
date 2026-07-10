using FluentAssertions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Comm;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Comm;

/// <summary>AC-CM4: India-compliance mode (dltRequired, the default) hard-blocks any SMS send without a DLT-registered template.</summary>
public sealed class SmsServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private static async Task<Guid> SeedTwilioConfigAsync(LexFlowDbContext db, Guid tenantId, bool dltRequired = true)
    {
        var config = new GatewayConfig(tenantId, "sms_twilio", dltRequired ? "{}" : "{\"dltRequired\":false}");
        config.SetEnabled(true);
        await db.GatewayConfigs.AddAsync(config);
        await db.SaveChangesAsync();
        return config.Id;
    }

    [Fact]
    public async Task SendAsync_blocks_a_freeform_send_when_DLT_is_required()
    {
        await using var db = CreateContext(nameof(SendAsync_blocks_a_freeform_send_when_DLT_is_required));
        var tenantId = Guid.NewGuid();
        await SeedTwilioConfigAsync(db, tenantId);
        var service = new SmsService(db, [new FakeSmsProvider("sms_twilio", success: true)]);

        var act = () => service.SendAsync(tenantId, new SendSmsInput(null, null, "+919999999999", null, "Hi there, free text", null), CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "DLT_TEMPLATE_REQUIRED");
    }

    [Fact]
    public async Task SendAsync_blocks_a_template_without_a_DLT_id_when_DLT_is_required()
    {
        await using var db = CreateContext(nameof(SendAsync_blocks_a_template_without_a_DLT_id_when_DLT_is_required));
        var tenantId = Guid.NewGuid();
        await SeedTwilioConfigAsync(db, tenantId);
        var template = new CommTemplate(tenantId, "SMS", "Hearing reminder", "Your hearing is on {{date}}", "[\"date\"]");
        await db.CommTemplates.AddAsync(template);
        await db.SaveChangesAsync();
        var service = new SmsService(db, [new FakeSmsProvider("sms_twilio", success: true)]);

        var act = () => service.SendAsync(tenantId, new SendSmsInput(null, null, "+919999999999", template.Id, null, new Dictionary<string, string> { ["date"] = "12 July" }), CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "DLT_TEMPLATE_REQUIRED");
    }

    [Fact]
    public async Task SendAsync_allows_a_DLT_registered_template_send()
    {
        await using var db = CreateContext(nameof(SendAsync_allows_a_DLT_registered_template_send));
        var tenantId = Guid.NewGuid();
        await SeedTwilioConfigAsync(db, tenantId);
        var template = new CommTemplate(tenantId, "SMS", "Hearing reminder", "Your hearing is on {{date}}", "[\"date\"]");
        template.SetDltTemplateId("DLT-12345");
        await db.CommTemplates.AddAsync(template);
        await db.SaveChangesAsync();
        var service = new SmsService(db, [new FakeSmsProvider("sms_twilio", success: true)]);

        var result = await service.SendAsync(tenantId, new SendSmsInput(null, null, "+919999999999", template.Id, null, new Dictionary<string, string> { ["date"] = "12 July" }), CancellationToken.None);

        result.Body.Should().Be("Your hearing is on 12 July");
        result.DltTemplateId.Should().Be("DLT-12345");
        result.Status.Should().Be("Sent");
    }

    [Fact]
    public async Task SendAsync_allows_freeform_when_the_tenant_has_explicitly_disabled_DLT_enforcement()
    {
        await using var db = CreateContext(nameof(SendAsync_allows_freeform_when_the_tenant_has_explicitly_disabled_DLT_enforcement));
        var tenantId = Guid.NewGuid();
        await SeedTwilioConfigAsync(db, tenantId, dltRequired: false);
        var service = new SmsService(db, [new FakeSmsProvider("sms_twilio", success: true)]);

        var result = await service.SendAsync(tenantId, new SendSmsInput(null, null, "+15550001234", null, "Freeform, non-India tenant", null), CancellationToken.None);

        result.Status.Should().Be("Sent");
    }

    [Fact]
    public async Task SendAsync_throws_when_no_gateway_is_configured()
    {
        await using var db = CreateContext(nameof(SendAsync_throws_when_no_gateway_is_configured));
        var tenantId = Guid.NewGuid();
        var service = new SmsService(db, [new FakeSmsProvider("sms_twilio", success: true)]);

        var act = () => service.SendAsync(tenantId, new SendSmsInput(null, null, "+919999999999", null, "Hi", null), CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "SMS_NOT_CONFIGURED");
    }

    private sealed class FakeSmsProvider(string provider, bool success) : ISmsProvider
    {
        public string Provider => provider;

        public Task<SmsSendResult> SendAsync(Guid tenantId, string toNumber, string body, string? dltTemplateId, CancellationToken cancellationToken = default)
            => Task.FromResult(success ? new SmsSendResult(true, "SM123", "Sent", null) : new SmsSendResult(false, null, "Failed", "simulated failure"));
    }
}
