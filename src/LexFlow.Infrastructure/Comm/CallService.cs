using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Comm;

/// <summary>
/// Module 11 calls: manual log with an optional one-click follow-up task, and Twilio
/// Voice click-to-call — auto-logged on initiation (recording/duration are filled in
/// later by the webhook router calling <see cref="RecordCallStatusAsync"/>). Recording
/// requires the caller's explicit consent flag (also DB-enforced:
/// ck_call_logs_recording_requires_consent).
/// </summary>
public sealed class CallService(LexFlowDbContext db, ITaskService taskService, IHttpClientFactory httpClientFactory, GatewayCredentialResolver credentials) : ICallService
{
    private const string VoiceProvider = "voice_twilio";

    public async Task<CallLogDto> LogAsync(Guid tenantId, Guid? actorId, LogCallInput input, CancellationToken cancellationToken = default)
    {
        var call = new CallLog(tenantId, input.ClientId, input.MatterId, input.UserId ?? actorId, input.Direction, input.DurationSec, input.Summary, followUpTaskId: null, provider: null, providerCallId: null, consentGiven: false);

        if (input.CreateFollowUpTask && !string.IsNullOrWhiteSpace(input.FollowUpTaskTitle))
        {
            var task = await taskService.CreateAsync(tenantId, actorId, new CreateOpsTaskInput(input.FollowUpTaskTitle, input.Summary, input.MatterId, input.ClientId, actorId, DateTimeOffset.UtcNow.AddDays(1), "Medium", "Follow-up"), cancellationToken);
            call = new CallLog(tenantId, input.ClientId, input.MatterId, input.UserId ?? actorId, input.Direction, input.DurationSec, input.Summary, task.Id, provider: null, providerCallId: null, consentGiven: false);
        }

        await db.CallLogs.AddAsync(call, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(call);
    }

    public async Task<IReadOnlyList<CallLogDto>> GetForClientAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default)
    {
        var calls = await db.CallLogs.Where(c => c.TenantId == tenantId && c.ClientId == clientId).OrderByDescending(c => c.OccurredAt).ToListAsync(cancellationToken);
        return calls.Select(ToDto).ToList();
    }

    public async Task<CallLogDto> ClickToCallAsync(Guid tenantId, Guid actorId, ClickToCallInput input, CancellationToken cancellationToken = default)
    {
        var resolved = await credentials.ResolveAsync(tenantId, VoiceProvider, cancellationToken);
        if (resolved is null)
        {
            throw new DomainRuleException("VOICE_NOT_CONFIGURED", "Twilio Voice is not configured and enabled for this tenant.");
        }

        var (config, authToken) = resolved.Value;
        using var configDoc = JsonDocument.Parse(config.ConfigJson);
        var accountSid = configDoc.RootElement.TryGetProperty("accountSid", out var sidProp) ? sidProp.GetString() : null;
        var senderId = configDoc.RootElement.TryGetProperty("senderId", out var senderProp) ? senderProp.GetString() : null;
        var twimlUrl = configDoc.RootElement.TryGetProperty("twimlUrl", out var twimlProp) ? twimlProp.GetString() : null;

        if (string.IsNullOrWhiteSpace(accountSid) || string.IsNullOrWhiteSpace(authToken))
        {
            throw new DomainRuleException("VOICE_NOT_CONFIGURED", "Twilio Voice Account SID and Auth Token are required.");
        }

        string? providerCallId = null;
        try
        {
            using var client = httpClientFactory.CreateClient();
            var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{accountSid}:{authToken}"));
            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Calls.json")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Basic", basicAuth) },
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["To"] = input.ToNumber,
                    ["From"] = senderId ?? string.Empty,
                    ["Url"] = twimlUrl ?? "https://demo.twilio.com/welcome/voice/",
                    ["Record"] = input.ConsentGiven ? "true" : "false",
                }),
            };

            using var response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
                providerCallId = body.TryGetProperty("sid", out var sidResultProp) ? sidResultProp.GetString() : null;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new DomainRuleException("VOICE_CALL_FAILED", $"Twilio Voice call initiation failed: {ex.Message}");
        }

        var call = new CallLog(tenantId, input.ClientId, input.MatterId, actorId, "Outbound", 0, null, followUpTaskId: null, VoiceProvider, providerCallId, input.ConsentGiven);
        await db.CallLogs.AddAsync(call, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(call);
    }

    public async Task RecordCallStatusAsync(Guid tenantId, string providerCallId, int durationSec, string? recordingBlobPath, CancellationToken cancellationToken = default)
    {
        var call = await db.CallLogs.SingleOrDefaultAsync(c => c.TenantId == tenantId && c.ProviderCallId == providerCallId, cancellationToken);
        if (call is null)
        {
            return;
        }

        call.SetDuration(durationSec);
        if (recordingBlobPath is not null && call.ConsentGiven)
        {
            call.SetRecording(recordingBlobPath);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static CallLogDto ToDto(CallLog c) => new(c.Id, c.ClientId, c.MatterId, c.UserId, c.Direction, c.DurationSec, c.Summary, c.FollowUpTaskId, c.ConsentGiven, c.RecordingBlobPath, c.OccurredAt);
}
