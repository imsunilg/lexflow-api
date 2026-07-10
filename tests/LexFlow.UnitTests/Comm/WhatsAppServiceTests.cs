using FluentAssertions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Comm;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Comm;

/// <summary>Module 11: template sends require an active opt-in; session sends require an open 24h window.</summary>
public sealed class WhatsAppServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private static async Task<Client> SeedClientAsync(LexFlowDbContext db, Guid tenantId)
    {
        var client = new Client(tenantId, "CL-1", "Individual", "Priya", "Shah", null, null, "+919999999999", null, null, null, null, null);
        await db.Clients.AddAsync(client);
        await db.SaveChangesAsync();
        return client;
    }

    [Fact]
    public async Task SendAsync_blocks_a_template_send_without_an_active_opt_in()
    {
        await using var db = CreateContext(nameof(SendAsync_blocks_a_template_send_without_an_active_opt_in));
        var tenantId = Guid.NewGuid();
        var client = await SeedClientAsync(db, tenantId);
        var template = new CommTemplate(tenantId, "WhatsApp", "Hearing reminder", "Your hearing is on {{date}}", "[\"date\"]");
        template.SetWaHsmName("hearing_reminder_v1");
        await db.CommTemplates.AddAsync(template);
        await db.SaveChangesAsync();
        var service = new WhatsAppService(db, new FakeWhatsAppProvider(success: true));

        var act = () => service.SendAsync(tenantId, new SendWhatsAppInput(client.Id, template.Id, new Dictionary<string, string> { ["date"] = "12 July" }, null), CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "WHATSAPP_OPT_IN_REQUIRED");
    }

    [Fact]
    public async Task SendAsync_allows_a_template_send_once_the_client_has_opted_in()
    {
        await using var db = CreateContext(nameof(SendAsync_allows_a_template_send_once_the_client_has_opted_in));
        var tenantId = Guid.NewGuid();
        var client = await SeedClientAsync(db, tenantId);
        var template = new CommTemplate(tenantId, "WhatsApp", "Hearing reminder", "Your hearing is on {{date}}", "[\"date\"]");
        template.SetWaHsmName("hearing_reminder_v1");
        await db.CommTemplates.AddAsync(template);
        await db.SaveChangesAsync();
        var service = new WhatsAppService(db, new FakeWhatsAppProvider(success: true));
        await service.OptInAsync(tenantId, client.Id, "+919999999999", "portal", CancellationToken.None);

        var result = await service.SendAsync(tenantId, new SendWhatsAppInput(client.Id, template.Id, new Dictionary<string, string> { ["date"] = "12 July" }, null), CancellationToken.None);

        result.Status.Should().Be("Sent");
        result.TemplateId.Should().Be(template.Id);
    }

    [Fact]
    public async Task SendAsync_blocks_a_session_message_outside_the_24h_window()
    {
        await using var db = CreateContext(nameof(SendAsync_blocks_a_session_message_outside_the_24h_window));
        var tenantId = Guid.NewGuid();
        var client = await SeedClientAsync(db, tenantId);
        var service = new WhatsAppService(db, new FakeWhatsAppProvider(success: true));

        var act = () => service.SendAsync(tenantId, new SendWhatsAppInput(client.Id, null, null, "Just checking in"), CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "WHATSAPP_SESSION_WINDOW_CLOSED");
    }

    [Fact]
    public async Task SendAsync_allows_a_session_message_inside_an_open_window_from_a_prior_inbound_message()
    {
        await using var db = CreateContext(nameof(SendAsync_allows_a_session_message_inside_an_open_window_from_a_prior_inbound_message));
        var tenantId = Guid.NewGuid();
        var client = await SeedClientAsync(db, tenantId);
        var service = new WhatsAppService(db, new FakeWhatsAppProvider(success: true));
        await service.RecordInboundAsync(tenantId, client.Id, "wamid.inbound1", "Hi, I have a question", CancellationToken.None);

        var result = await service.SendAsync(tenantId, new SendWhatsAppInput(client.Id, null, null, "Sure, how can I help?"), CancellationToken.None);

        result.Status.Should().Be("Sent");
        result.Body.Should().Be("Sure, how can I help?");
    }

    [Fact]
    public async Task OptOutAsync_closes_the_opt_in_so_a_later_template_send_is_blocked_again()
    {
        await using var db = CreateContext(nameof(OptOutAsync_closes_the_opt_in_so_a_later_template_send_is_blocked_again));
        var tenantId = Guid.NewGuid();
        var client = await SeedClientAsync(db, tenantId);
        var service = new WhatsAppService(db, new FakeWhatsAppProvider(success: true));
        await service.OptInAsync(tenantId, client.Id, "+919999999999", "portal", CancellationToken.None);

        (await service.HasActiveOptInAsync(tenantId, client.Id, CancellationToken.None)).Should().BeTrue();

        await service.OptOutAsync(tenantId, client.Id, CancellationToken.None);

        (await service.HasActiveOptInAsync(tenantId, client.Id, CancellationToken.None)).Should().BeFalse();
    }

    private sealed class FakeWhatsAppProvider(bool success) : IWhatsAppProvider
    {
        public Task<WhatsAppSendResult> SendSessionMessageAsync(Guid tenantId, string toPhoneE164, string body, CancellationToken cancellationToken = default)
            => Task.FromResult(success ? new WhatsAppSendResult(true, "wamid.session1", "Sent", null) : new WhatsAppSendResult(false, null, "Failed", "simulated failure"));

        public Task<WhatsAppSendResult> SendTemplateMessageAsync(Guid tenantId, string toPhoneE164, string hsmName, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken = default)
            => Task.FromResult(success ? new WhatsAppSendResult(true, "wamid.template1", "Sent", null) : new WhatsAppSendResult(false, null, "Failed", "simulated failure"));
    }
}
