using Hangfire;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Ai;

/// <summary>
/// See IAiTranscriptionService's own doc comment. RequestAsync stores the audio blob and enqueues
/// ProcessAsync as a Hangfire job (same "API enqueues, Workers executes" split as
/// ILeadImportService/IDocumentService); ProcessAsync is that job's body.
/// </summary>
public sealed class AiTranscriptionService(
    LexFlowDbContext db,
    IBlobStorageService blobStorage,
    IAvScanner avScanner,
    ISpeechToTextService speechToText,
    IAiQuotaService quotaService,
    IAiInteractionAuditService auditService,
    IJobsBroadcaster jobsBroadcaster,
    IBackgroundJobClient backgroundJobClient) : IAiTranscriptionService
{
    private const string Container = "ai-transcriptions";
    private const decimal CreditsPerTranscription = 3;

    public async Task<AiTranscriptionDto> RequestAsync(Guid tenantId, Guid requestedBy, Guid? matterId, byte[] audioContent, string fileName, string contentType, string? language, CancellationToken cancellationToken = default)
    {
        await quotaService.EnsureWithinQuotaAsync(tenantId, CreditsPerTranscription, cancellationToken);

        var scan = await avScanner.ScanAsync(new MemoryStream(audioContent), fileName, cancellationToken);
        if (!scan.IsClean)
        {
            throw new DomainRuleException("MALWARE_DETECTED", $"Uploaded audio '{fileName}' failed the antivirus scan.");
        }

        var blobPath = $"{tenantId:N}/{Guid.NewGuid():N}-{fileName}";
        await blobStorage.UploadAsync(Container, blobPath, audioContent, contentType, cancellationToken);

        var transcription = new AiTranscription(tenantId, matterId, requestedBy, blobPath, null, language);
        await db.AiTranscriptions.AddAsync(transcription, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        backgroundJobClient.Enqueue<IAiTranscriptionService>(s => s.ProcessAsync(tenantId, transcription.Id, CancellationToken.None));

        return ToDto(transcription);
    }

    public async Task<AiTranscriptionDto?> GetAsync(Guid tenantId, Guid transcriptionId, CancellationToken cancellationToken = default)
    {
        var transcription = await db.AiTranscriptions.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Id == transcriptionId, cancellationToken);
        return transcription is null ? null : ToDto(transcription);
    }

    public async Task ProcessAsync(Guid tenantId, Guid transcriptionId, CancellationToken cancellationToken = default)
    {
        var transcription = await db.AiTranscriptions.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Id == transcriptionId, cancellationToken)
            ?? throw new NotFoundException(nameof(AiTranscription), transcriptionId);

        transcription.Start();
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var audio = await blobStorage.DownloadAsync(Container, transcription.BlobPath, cancellationToken);
            var result = await speechToText.TranscribeAsync(audio, "audio/mpeg", transcription.Language, cancellationToken);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
            {
                transcription.Fail(result.ErrorMessage ?? "Transcription produced no text.");
            }
            else
            {
                transcription.Complete(result.Text);
                await auditService.RecordAsync(
                    new AiInteractionRecord(tenantId, "voice-transcription", "speech-to-text", "1.0.0", "speech-to-text", 0, 0, 0, CreditsPerTranscription, transcription.RequestedBy, "AiTranscription", transcriptionId, null, result.Text),
                    cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            transcription.Fail(ex.Message);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (transcription.RequestedBy.HasValue)
        {
            await jobsBroadcaster.NotifyJobCompletedAsync(tenantId, transcription.RequestedBy.Value, "ai-transcription", transcriptionId, transcription.Status, cancellationToken);
        }
    }

    private static AiTranscriptionDto ToDto(AiTranscription t) => new(t.Id, t.Status, t.TranscriptText, t.DurationSec, t.Language, t.ErrorMessage);
}
