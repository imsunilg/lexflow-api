namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// ClamAV sidecar client (Module 7 pipeline: "AV scan → store blob..."). Behind an
/// interface so it's mockable in tests — no test in this codebase should ever open a
/// real socket to a ClamAV daemon. Error Handling: "AV positive → quarantine bucket,
/// uploader notified, admin alert, audit."
/// </summary>
public interface IAvScanner
{
    Task<AvScanResult> ScanAsync(Stream content, string fileName, CancellationToken cancellationToken = default);
}

public sealed record AvScanResult(bool IsClean, string? ThreatName);
