namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 16 feature #10: "Whisper-class STT ... legal-vocabulary custom lexicon; Hindi+English
/// code-switch support." Interface-only here — real Whisper/Azure Speech wiring is provider
/// infrastructure this repo doesn't have credentials for, same simplification already
/// established for IEmailNotificationProvider/ISmsNotificationProvider/IWhatsAppNotificationProvider
/// (Module 6/10): DI registers a logging no-op so AiTranscriptionService's own pipeline logic
/// (status machine, notify, quota) is fully exercised today.
/// </summary>
public interface ISpeechToTextService
{
    Task<SpeechToTextResult> TranscribeAsync(byte[] audioContent, string contentType, string? language, CancellationToken cancellationToken = default);
}

public sealed record SpeechToTextResult(bool Success, string? Text, string? ErrorMessage);
