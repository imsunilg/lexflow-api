using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using LexFlow.Application.Common.Interfaces;

namespace LexFlow.Infrastructure.Storage;

/// <summary>Thin wrapper over the shared <see cref="BlobServiceClient"/> singleton (per-tenant containers, PRD §20(5)).</summary>
public sealed class BlobStorageService(BlobServiceClient blobServiceClient) : IBlobStorageService
{
    public async Task<string> UploadAsync(string container, string blobPath, byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        var containerClient = blobServiceClient.GetBlobContainerClient(container);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobClient = containerClient.GetBlobClient(blobPath);
        using var stream = new MemoryStream(content);
        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken: cancellationToken);
        return blobPath;
    }

    public async Task<byte[]> DownloadAsync(string container, string blobPath, CancellationToken cancellationToken = default)
    {
        var containerClient = blobServiceClient.GetBlobContainerClient(container);
        var blobClient = containerClient.GetBlobClient(blobPath);
        var download = await blobClient.DownloadContentAsync(cancellationToken);
        return download.Value.Content.ToArray();
    }

    public Task<string> GetDownloadUrlAsync(string container, string blobPath, TimeSpan validFor, CancellationToken cancellationToken = default)
    {
        var containerClient = blobServiceClient.GetBlobContainerClient(container);
        var blobClient = containerClient.GetBlobClient(blobPath);

        if (!blobClient.CanGenerateSasUri)
        {
            // No shared-key credential available to this client (e.g. a token-credential-only
            // connection, or a test double) — fall back to the plain blob URL rather than
            // failing the download outright.
            return Task.FromResult(blobClient.Uri.ToString());
        }

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = container,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(validFor),
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return Task.FromResult(blobClient.GenerateSasUri(sasBuilder).ToString());
    }
}
