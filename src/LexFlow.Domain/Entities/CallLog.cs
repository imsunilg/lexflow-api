using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to comm.call_logs (lexflow-database Scripts/08_Comm/CallLogs).</summary>
public sealed class CallLog : AuditableEntity
{
    private CallLog()
    {
    }

    public CallLog(Guid tenantId, Guid? clientId, Guid? matterId, Guid? userId, string direction, int durationSec, string? summary, Guid? followUpTaskId, string? provider, string? providerCallId, bool consentGiven)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ClientId = clientId;
        MatterId = matterId;
        UserId = userId;
        Direction = direction;
        DurationSec = durationSec;
        Summary = summary;
        FollowUpTaskId = followUpTaskId;
        Provider = provider;
        ProviderCallId = providerCallId;
        ConsentGiven = consentGiven;
        OccurredAt = DateTimeOffset.UtcNow;
    }

    public Guid? ClientId { get; private set; }
    public Guid? MatterId { get; private set; }
    public Guid? UserId { get; private set; }
    public string Direction { get; private set; } = null!;
    public int DurationSec { get; private set; }
    public string? Summary { get; private set; }
    public Guid? FollowUpTaskId { get; private set; }
    public string? RecordingBlobPath { get; private set; }
    public bool ConsentGiven { get; private set; }
    public string? Provider { get; private set; }
    public string? ProviderCallId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>DB-enforced too (ck_call_logs_recording_requires_consent): a recording path can only be set once consent is on.</summary>
    public void SetRecording(string blobPath)
    {
        if (!ConsentGiven)
        {
            throw new InvalidOperationException("A recording cannot be attached without recording consent.");
        }

        RecordingBlobPath = blobPath;
    }

    public void SetDuration(int durationSec) => DurationSec = durationSec;
}
