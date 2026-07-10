using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to ai.ai_transcriptions (lexflow-database Scripts/18_AI/AiTranscriptions).
/// Module 16 feature #10/#11: async Whisper-class transcription pipeline. Status machine:
/// Queued -&gt; Processing -&gt; Done|Failed, driven by AiTranscriptionService's Hangfire job.
/// </summary>
public sealed class AiTranscription : Entity
{
    private AiTranscription()
    {
    }

    public AiTranscription(Guid tenantId, Guid? matterId, Guid? requestedBy, string blobPath, int? durationSec, string? language)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        MatterId = matterId;
        RequestedBy = requestedBy;
        Status = "Queued";
        BlobPath = blobPath;
        DurationSec = durationSec;
        Language = language;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid TenantId { get; private set; }
    public Guid? MatterId { get; private set; }
    public Guid? RequestedBy { get; private set; }
    public string Status { get; private set; } = "Queued";
    public string BlobPath { get; private set; } = null!;
    public int? DurationSec { get; private set; }
    public string? Language { get; private set; }
    public string? TranscriptText { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public void Start() => Status = "Processing";

    public void Complete(string transcriptText)
    {
        Status = "Done";
        TranscriptText = transcriptText;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Fail(string errorMessage)
    {
        Status = "Failed";
        ErrorMessage = errorMessage;
        CompletedAt = DateTimeOffset.UtcNow;
    }
}
