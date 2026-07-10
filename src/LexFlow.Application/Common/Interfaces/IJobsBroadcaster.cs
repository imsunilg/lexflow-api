namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the SignalR /hubs/jobs push (PRD §16 Realtime: "background job progress
/// [OCR, imports, AI jobs]") so Infrastructure never references the Hub type itself — same
/// dependency-direction reasoning as IChatBroadcaster/ChatHub. The Api project registers the
/// real SignalR-backed implementation. Module 16: "the Whisper-class transcription pipeline
/// (async, Hangfire job, webhook/SignalR notify on completion)" is the first consumer.
/// </summary>
public interface IJobsBroadcaster
{
    Task NotifyJobCompletedAsync(Guid tenantId, Guid userId, string jobKind, Guid jobId, string status, CancellationToken cancellationToken = default);
}
