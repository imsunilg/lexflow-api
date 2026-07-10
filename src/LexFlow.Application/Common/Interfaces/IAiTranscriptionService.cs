namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 16 features #10 (Voice Notes to Text) / #11 (Meeting Summary): "Whisper-class STT ->
/// formatted note attached to matter." Async pipeline: <see cref="RequestAsync"/> stores the
/// audio blob and queues a Hangfire job; <see cref="ProcessAsync"/> is that job's body — it
/// transcribes and pushes a SignalR notify (IJobsBroadcaster) on completion, per Module 16
/// APIs: "POST /api/v1/ai/transcribe (multipart audio)."
/// </summary>
public interface IAiTranscriptionService
{
    Task<AiTranscriptionDto> RequestAsync(Guid tenantId, Guid requestedBy, Guid? matterId, byte[] audioContent, string fileName, string contentType, string? language, CancellationToken cancellationToken = default);

    Task<AiTranscriptionDto?> GetAsync(Guid tenantId, Guid transcriptionId, CancellationToken cancellationToken = default);

    /// <summary>Hangfire job body — enqueued against this same interface, exactly like ILeadImportService.</summary>
    Task ProcessAsync(Guid tenantId, Guid transcriptionId, CancellationToken cancellationToken = default);
}

public sealed record AiTranscriptionDto(Guid Id, string Status, string? TranscriptText, int? DurationSec, string? Language, string? ErrorMessage) : AiGeneratedResponse;
