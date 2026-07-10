namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 11 calls — manual log plus Twilio Voice click-to-call (auto-logged on connect, per-jurisdiction recording consent).</summary>
public interface ICallService
{
    Task<CallLogDto> LogAsync(Guid tenantId, Guid? actorId, LogCallInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CallLogDto>> GetForClientAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default);

    /// <summary>Initiates a Twilio Voice call connecting the calling user to the client's number; the resulting call auto-logs (consent gate on recording per Security Rules).</summary>
    Task<CallLogDto> ClickToCallAsync(Guid tenantId, Guid actorId, ClickToCallInput input, CancellationToken cancellationToken = default);

    Task RecordCallStatusAsync(Guid tenantId, string providerCallId, int durationSec, string? recordingBlobPath, CancellationToken cancellationToken = default);
}

public sealed record LogCallInput(Guid? ClientId, Guid? MatterId, Guid? UserId, string Direction, int DurationSec, string? Summary, bool CreateFollowUpTask, string? FollowUpTaskTitle);

public sealed record ClickToCallInput(Guid? ClientId, Guid? MatterId, string ToNumber, bool ConsentGiven);

public sealed record CallLogDto(Guid Id, Guid? ClientId, Guid? MatterId, Guid? UserId, string Direction, int DurationSec, string? Summary, Guid? FollowUpTaskId, bool ConsentGiven, string? RecordingBlobPath, DateTimeOffset OccurredAt);
