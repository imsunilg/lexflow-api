using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.IntegrationTests.Smoke;

/// <summary>
/// §37 Testing Strategy critical journeys, driven end to end over real HTTP against a
/// Postgres+Redis+ES Testcontainers-backed host (see SmokeTestFixture):
///   1. lead → convert → matter → hearing → outcome
///   2. WIP (time entry) → invoice → pay
/// "Pay" here is the staff-side POST /api/v1/payments record-payment flow (Module 8), not the
/// client portal's Pay-Now gateway-checkout flow (Module 17, already covered by
/// PortalPayNowServiceTests) — both are legitimate readings of "WIP→invoice→pay" and this one
/// is the one that doesn't require simulating a third-party payment gateway callback.
/// </summary>
[Collection(nameof(SmokeTestCollection))]
public sealed class CriticalJourneysSmokeTests(SmokeTestFixture fixture)
{
    [Fact]
    public async Task Lead_to_convert_to_matter_to_hearing_to_outcome_journey_completes_end_to_end()
    {
        var tenantId = Guid.NewGuid();
        var court = await SeedCourtAsync(tenantId);
        var token = await SeedStaffUserAndMintTokenAsync(tenantId,
        [
            "leads.create.all", "leads.read.all", "leads.manage.all",
            "matters.manage.all", "matters.read.all",
        ]);

        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var lead = await PostAsync<LeadResponse>(client, "/api/v1/leads", new
        {
            firstName = "Journey",
            lastName = "Lead",
            company = (string?)null,
            email = "journey-lead@example.com",
            phoneE164 = (string?)null,
            sourceId = (Guid?)null,
            practiceAreaId = (Guid?)null,
            issueSummary = "Smoke test matter",
            opposingParty = (string?)null,
            budgetBand = (string?)null,
        });

        var convert = await PostAsync<ConvertResponse>(client, $"/api/v1/leads/{lead.Id}/convert", new
        {
            createMatter = false,
            matterPayload = (string?)null,
            invoicePayload = (string?)null,
        });
        convert.ClientId.Should().NotBeEmpty();

        var matter = await PostAsync<MatterResponse>(client, "/api/v1/matters", new
        {
            title = "Journey Matter",
            clientId = convert.ClientId,
            matterType = "Litigation",
            practiceAreaId = (Guid?)null,
            branchId = (Guid?)null,
            responsibleLawyerId = (Guid?)null,
            priority = "Medium",
            description = (string?)null,
            openedOn = DateOnly.FromDateTime(DateTime.UtcNow),
            isPrivate = false,
            budget = (decimal?)null,
            billingArrangementJson = (string?)null,
            oppositePartyNames = (IReadOnlyList<string>?)null,
            overrideConflict = false,
            conflictOverrideReason = (string?)null,
        });

        var courtCase = await PostAsync<CourtCaseResponse>(client, $"/api/v1/matters/{matter.Id}/cases", new
        {
            courtId = court.Id,
            caseType = "CS",
            caseNumber = "SMOKE-0001",
            caseYear = DateTime.UtcNow.Year,
            cnrNumber = (string?)null,
            filingDate = (DateOnly?)null,
            stage = (string?)null,
            judgeId = (Guid?)null,
            courtroom = (string?)null,
        });

        var hearing = await PostAsync<HearingResponse>(client, $"/api/v1/cases/{courtCase.Id}/hearings", new
        {
            date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            time = (TimeOnly?)null,
            purpose = "First hearing",
            courtroom = (string?)null,
            assignedLawyerId = (Guid?)null,
        });

        var outcome = await PostAsync<RecordOutcomeResultResponse>(client, $"/api/v1/hearings/{hearing.Id}/outcome", new
        {
            summary = "Matter heard and disposed.",
            adjournReason = (string?)null,
            nextHearing = (object?)null,
            sineDie = false,
            disposed = true,
            orders = (object?)null,
            createComplianceTask = false,
        });

        outcome.NextHearing.Should().BeNull("the case was disposed, not adjourned");

        await using var db = CreateDbContext();
        var persistedCase = await db.CourtCases.SingleAsync(c => c.Id == courtCase.Id);
        persistedCase.Status.Should().Be("Disposed");
    }

    [Fact]
    public async Task WIP_to_invoice_to_pay_journey_completes_end_to_end()
    {
        var tenantId = Guid.NewGuid();
        var (client_, matterId) = await SeedClientAndMatterAsync(tenantId);

        var lawyerToken = await SeedStaffUserAndMintTokenAsync(tenantId,
        [
            "time_entries.create.own", "time_entries.read.own", "time_entries.update.own",
            "invoices.create.own", "invoices.read.own", "invoices.update.own",
        ]);
        var managerToken = await SeedStaffUserAndMintTokenAsync(tenantId,
        [
            "time_entries.approve.team", "invoices.approve.team", "invoices.send.all",
            "payments.record.all", "payments.read.all",
        ]);

        using var lawyerClient = fixture.CreateClient();
        lawyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", lawyerToken);
        using var managerClient = fixture.CreateClient();
        managerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);

        var timeEntry = await PostAsync<TimeEntryResponse>(lawyerClient, "/api/v1/time-entries", new
        {
            matterId,
            activityCodeId = (Guid?)null,
            entryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            startedAt = (DateTimeOffset?)null,
            durationMin = 60,
            billable = true,
            narrative = "Smoke test WIP",
            internalNote = (string?)null,
        });

        await PostAsync<object>(lawyerClient, "/api/v1/time-entries/submit", new { ids = new[] { timeEntry.Id } });

        // Segregation of duties (Security Rules: "time.approve cannot approve own entries") —
        // approved by the manager, never the entry's own submitter. A manual rate override
        // sidesteps needing to also seed a RateCard/RateCardLine (BR-7's own fallback chain)
        // just to prove this journey, which is about the WIP->invoice->pay wiring, not rate
        // resolution (already covered elsewhere).
        var approvedEntries = await PostAsync<List<TimeEntryResponse>>(managerClient, "/api/v1/time-entries/approve", new { ids = new[] { timeEntry.Id }, manualRateOverride = 5000m });
        approvedEntries.Should().ContainSingle(e => e.Id == timeEntry.Id && e.Status == "Approved");

        var invoice = await PostAsync<InvoiceResponse>(lawyerClient, "/api/v1/invoices", new
        {
            matterId,
            issueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            dueInDays = 30,
            pullTimeEntryIds = new[] { timeEntry.Id },
            extraLines = (object?)null,
            discount = (object?)null,
            notes = (string?)null,
        });
        invoice.GrandTotal.Should().BeGreaterThan(0);

        await PostAsync<object>(lawyerClient, $"/api/v1/invoices/{invoice.Id}/submit", new { });
        await PostAsync<object>(managerClient, $"/api/v1/invoices/{invoice.Id}/approve", new { });
        await PostAsync<object>(managerClient, $"/api/v1/invoices/{invoice.Id}/send", new { });

        var payment = await PostAsync<PaymentResponse>(managerClient, "/api/v1/payments", new
        {
            clientId = client_,
            amount = invoice.GrandTotal,
            mode = "Bank Transfer",
            gateway = (string?)null,
            gatewayRef = (string?)null,
            receivedOn = DateOnly.FromDateTime(DateTime.UtcNow),
            allocations = new[] { new { invoiceId = invoice.Id, amount = invoice.GrandTotal } },
            idempotencyKey = (string?)null,
        });
        payment.Amount.Should().Be(invoice.GrandTotal);

        await using var db = CreateDbContext();
        var persistedInvoice = await db.Invoices.SingleAsync(i => i.Id == invoice.Id);
        persistedInvoice.Status.Should().Be("Paid");
        persistedInvoice.AmountPaid.Should().Be(invoice.GrandTotal);
    }

    private async Task<Court> SeedCourtAsync(Guid tenantId)
    {
        await using var db = CreateDbContext();
        var court = new Court(tenantId, "Smoke Test Court", "District", "Mumbai", "Maharashtra", null);
        await db.Courts.AddAsync(court);
        await db.SaveChangesAsync();
        return court;
    }

    private async Task<(Guid ClientId, Guid MatterId)> SeedClientAndMatterAsync(Guid tenantId)
    {
        await using var db = CreateDbContext();
        var client = new Client(tenantId, "CL-SMOKE-0001", "Individual", "Smoke", "Client", null, "smoke-client@example.com", null, null, null, null, null, null);
        var matter = new Matter(tenantId, "MAT-SMOKE-0001", "WIP smoke matter", client.Id, "Litigation", null, null, null, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        db.AddRange(client, matter);
        await db.SaveChangesAsync();
        return (client.Id, matter.Id);
    }

    private async Task<string> SeedStaffUserAndMintTokenAsync(Guid tenantId, IReadOnlyList<string> permissionKeys)
    {
        await using var db = CreateDbContext();
        var user = new User(tenantId, $"smoke-{Guid.NewGuid():N}@example.com", "Smoke User");
        user.Activate();
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();

        var permissions = new List<Permission>();
        var grants = new List<UserPermissionGrant>();
        foreach (var key in permissionKeys)
        {
            var parts = key.Split('.');
            var permission = new Permission(tenantId, key, parts[0], parts[1], parts[2]);
            permissions.Add(permission);
            grants.Add(new UserPermissionGrant(tenantId, user.Id, permission.Id));
        }

        await db.Permissions.AddRangeAsync(permissions);
        await db.UserPermissionGrants.AddRangeAsync(grants);
        await db.SaveChangesAsync();

        using var scope = fixture.Services.CreateScope();
        var jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var token = jwtTokenService.IssueAccessToken(user.Id, tenantId, "owner", null, permissionKeys, audience: "staff");
        return token.Token;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static async Task<TResponse> PostAsync<TResponse>(HttpClient client, string url, object body)
    {
        using var response = await client.PostAsJsonAsync(url, body, JsonOptions);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<TResponse>>(JsonOptions);
        return envelope!.Data!;
    }

    private LexFlowDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LexFlowDbContext>().UseNpgsql(fixture.ConnectionString).Options);

    private sealed record ApiEnvelope<T>(bool Success, T? Data);

    private sealed record LeadResponse(Guid Id);
    private sealed record ConvertResponse(Guid ClientId, Guid? MatterId, Guid? InvoiceId);
    private sealed record MatterResponse(Guid Id);
    private sealed record CourtCaseResponse(Guid Id);
    private sealed record HearingResponse(Guid Id);
    private sealed record RecordOutcomeResultResponse(HearingResponse? NextHearing);
    private sealed record TimeEntryResponse(Guid Id, string Status);
    private sealed record InvoiceResponse(Guid Id, decimal GrandTotal);
    private sealed record PaymentResponse(Guid Id, decimal Amount);
}
