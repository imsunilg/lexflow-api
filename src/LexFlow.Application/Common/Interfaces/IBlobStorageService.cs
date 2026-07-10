namespace LexFlow.Application.Common.Interfaces;

/// <summary>Thin wrapper over Azure.Storage.Blobs (per-tenant containers, PRD §20(5)) so Application never references the Azure SDK directly.</summary>
public interface IBlobStorageService
{
    Task<string> UploadAsync(string container, string blobPath, byte[] content, string contentType, CancellationToken cancellationToken = default);

    Task<byte[]> DownloadAsync(string container, string blobPath, CancellationToken cancellationToken = default);

    /// <summary>Module 7: "GET /documents/{id}/download → 302 SAS". Returns a time-limited, read-only URL; falls back to a plain blob URL when the underlying client can't mint a SAS (e.g. Azurite without a shared key, or a non-Azure test double).</summary>
    Task<string> GetDownloadUrlAsync(string container, string blobPath, TimeSpan validFor, CancellationToken cancellationToken = default);
}
