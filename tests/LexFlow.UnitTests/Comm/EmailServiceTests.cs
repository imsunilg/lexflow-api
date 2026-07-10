using FluentAssertions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Comm;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Comm;

/// <summary>AC-CM3: BCC-dropbox sender-match filing + Triage queue for ambiguous/unmatched senders.</summary>
public sealed class EmailServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private static InboundEmailInput Inbound(string fromAddr, string messageIdHdr) =>
        new(messageIdHdr, null, fromAddr, ["intake@in.lexflow.app"], "Re: my case", "<p>Hello</p>", DateTimeOffset.UtcNow, null);

    [Fact]
    public async Task HandleInboundAsync_files_directly_when_sender_matches_exactly_one_client_with_one_open_matter()
    {
        await using var db = CreateContext(nameof(HandleInboundAsync_files_directly_when_sender_matches_exactly_one_client_with_one_open_matter));
        var service = new EmailService(db, [], new InboundEmailHandler(db));
        var tenantId = Guid.NewGuid();

        var client = new Client(tenantId, "CL-1", "Individual", "Priya", "Shah", null, "priya@example.com", null, null, null, null, null, null);
        await db.Clients.AddAsync(client);
        var matter = new Matter(tenantId, "MAT-1", "Test matter", client.Id, "Litigation", null, null, null, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        await db.Matters.AddAsync(matter);
        await db.SaveChangesAsync();

        var thread = await service.HandleInboundAsync(tenantId, Inbound("priya@example.com", "<msg1@client>"), CancellationToken.None);

        thread.ClientId.Should().Be(client.Id);
        thread.MatterId.Should().Be(matter.Id);
    }

    [Fact]
    public async Task HandleInboundAsync_leaves_thread_unmatched_for_triage_when_sender_matches_no_client()
    {
        await using var db = CreateContext(nameof(HandleInboundAsync_leaves_thread_unmatched_for_triage_when_sender_matches_no_client));
        var service = new EmailService(db, [], new InboundEmailHandler(db));
        var tenantId = Guid.NewGuid();

        var thread = await service.HandleInboundAsync(tenantId, Inbound("stranger@example.com", "<msg2@client>"), CancellationToken.None);

        thread.ClientId.Should().BeNull();
        thread.MatterId.Should().BeNull();

        var triage = await service.GetTriageQueueAsync(tenantId, CancellationToken.None);
        triage.Should().ContainSingle(t => t.Id == thread.Id);
    }

    [Fact]
    public async Task HandleInboundAsync_leaves_thread_unmatched_for_triage_when_two_clients_share_the_same_address()
    {
        await using var db = CreateContext(nameof(HandleInboundAsync_leaves_thread_unmatched_for_triage_when_two_clients_share_the_same_address));
        var service = new EmailService(db, [], new InboundEmailHandler(db));
        var tenantId = Guid.NewGuid();

        var sharedEmail = "family@example.com";
        await db.Clients.AddAsync(new Client(tenantId, "CL-1", "Individual", "Ravi", "Shah", null, sharedEmail, null, null, null, null, null, null));
        await db.Clients.AddAsync(new Client(tenantId, "CL-2", "Individual", "Priya", "Shah", null, sharedEmail, null, null, null, null, null, null));
        await db.SaveChangesAsync();

        var thread = await service.HandleInboundAsync(tenantId, Inbound(sharedEmail, "<msg3@client>"), CancellationToken.None);

        thread.ClientId.Should().BeNull("two clients share this address — Module 11 edge case: 'thread linking prompts choice'");
        thread.MatterId.Should().BeNull();
    }

    [Fact]
    public async Task HandleInboundAsync_matches_via_client_contact_email_too()
    {
        await using var db = CreateContext(nameof(HandleInboundAsync_matches_via_client_contact_email_too));
        var service = new EmailService(db, [], new InboundEmailHandler(db));
        var tenantId = Guid.NewGuid();

        var client = new Client(tenantId, "CL-1", "Corporate", null, null, "Acme Pvt Ltd", null, null, null, null, null, null, null);
        await db.Clients.AddAsync(client);
        await db.ClientContacts.AddAsync(new ClientContact(tenantId, client.Id, "Ops Manager", "Manager", "ops@acme.example", null, true));
        var matter = new Matter(tenantId, "MAT-1", "Test matter", client.Id, "Litigation", null, null, null, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        await db.Matters.AddAsync(matter);
        await db.SaveChangesAsync();

        var thread = await service.HandleInboundAsync(tenantId, Inbound("ops@acme.example", "<msg4@client>"), CancellationToken.None);

        thread.ClientId.Should().Be(client.Id);
        thread.MatterId.Should().Be(matter.Id);
    }

    [Fact]
    public async Task LinkThreadToMatterAsync_resolves_a_triage_thread_and_backfills_its_messages()
    {
        await using var db = CreateContext(nameof(LinkThreadToMatterAsync_resolves_a_triage_thread_and_backfills_its_messages));
        var service = new EmailService(db, [], new InboundEmailHandler(db));
        var tenantId = Guid.NewGuid();

        var thread = await service.HandleInboundAsync(tenantId, Inbound("stranger@example.com", "<msg5@client>"), CancellationToken.None);

        var client = new Client(tenantId, "CL-1", "Individual", "New", "Client", null, null, null, null, null, null, null, null);
        await db.Clients.AddAsync(client);
        var matter = new Matter(tenantId, "MAT-2", "Test matter", client.Id, "Litigation", null, null, null, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        await db.Matters.AddAsync(matter);
        await db.SaveChangesAsync();

        var linked = await service.LinkThreadToMatterAsync(tenantId, thread.Id, matter.Id, client.Id, CancellationToken.None);

        linked.MatterId.Should().Be(matter.Id);
        var messages = await service.GetMessagesAsync(tenantId, thread.Id, CancellationToken.None);
        messages.Should().OnlyContain(m => true);
        (await db.EmailMessages.Where(m => m.ThreadId == thread.Id).ToListAsync()).Should().OnlyContain(m => m.MatterId == matter.Id);
    }

    [Fact]
    public async Task HandleInboundAsync_is_idempotent_for_a_redelivered_message_id()
    {
        await using var db = CreateContext(nameof(HandleInboundAsync_is_idempotent_for_a_redelivered_message_id));
        var service = new EmailService(db, [], new InboundEmailHandler(db));
        var tenantId = Guid.NewGuid();

        var first = await service.HandleInboundAsync(tenantId, Inbound("stranger@example.com", "<dup@client>"), CancellationToken.None);
        var second = await service.HandleInboundAsync(tenantId, Inbound("stranger@example.com", "<dup@client>"), CancellationToken.None);

        second.Id.Should().Be(first.Id);
        (await db.EmailMessages.CountAsync(m => m.MessageIdHdr == "<dup@client>")).Should().Be(1);
    }
}
