using LexFlow.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace LexFlow.Infrastructure.Ai;

/// <summary>See ISpeechToTextService's own doc comment: logging no-op standing in for a real Whisper/Azure Speech provider this repo has no credentials for.</summary>
public sealed class NullSpeechToTextService(ILogger<NullSpeechToTextService> logger) : ISpeechToTextService
{
    public Task<SpeechToTextResult> TranscribeAsync(byte[] audioContent, string contentType, string? language, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("NullSpeechToTextService: no real STT provider configured — {Bytes} bytes ({ContentType}) not transcribed.", audioContent.Length, contentType);
        return Task.FromResult(new SpeechToTextResult(false, null, "No speech-to-text provider is configured for this environment."));
    }
}
