using System.Net.Sockets;
using System.Text;
using LexFlow.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace LexFlow.Infrastructure.Dms;

/// <summary>
/// ClamAV sidecar client speaking the clamd INSTREAM protocol directly over TCP (no
/// clamd .NET client library is well-maintained, so this is a small direct
/// implementation of the wire protocol: "zINSTREAM\0" + length-prefixed chunks +
/// zero-length terminator, then read the single response line).
/// When no ClamAv:Host is configured (e.g. local dev without a clamd sidecar running),
/// scanning is skipped and every file is treated as clean — same conditional-external
/// pattern used elsewhere in this codebase (Key Vault, Elasticsearch) for optional
/// infra that isn't provisioned in every environment; production deployments must set
/// ClamAv:Host for the "AV scan → store blob" pipeline order to mean anything.
/// </summary>
public sealed class ClamAvScanner(IOptions<ClamAvOptions> options) : IAvScanner
{
    private const int ChunkSize = 8192;

    public async Task<AvScanResult> ScanAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        var clamOptions = options.Value;
        if (string.IsNullOrWhiteSpace(clamOptions.Host))
        {
            return new AvScanResult(true, null);
        }

        using var client = new TcpClient();
        await client.ConnectAsync(clamOptions.Host, clamOptions.Port, cancellationToken);
        await using var stream = client.GetStream();

        var command = Encoding.ASCII.GetBytes("zINSTREAM\0");
        await stream.WriteAsync(command, cancellationToken);

        if (content.CanSeek)
        {
            content.Seek(0, SeekOrigin.Begin);
        }

        var buffer = new byte[ChunkSize];
        int bytesRead;
        while ((bytesRead = await content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            var lengthPrefix = BitConverter.GetBytes(bytesRead);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(lengthPrefix);
            }

            await stream.WriteAsync(lengthPrefix, cancellationToken);
            await stream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        var zeroLength = new byte[4];
        await stream.WriteAsync(zeroLength, cancellationToken);

        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        var response = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;

        // clamd responses look like "stream: OK" or "stream: Eicar-Test-Signature FOUND".
        if (response.Contains("FOUND", StringComparison.Ordinal))
        {
            var threatName = response.Replace("stream:", string.Empty).Replace("FOUND", string.Empty).Trim();
            return new AvScanResult(false, threatName);
        }

        return new AvScanResult(true, null);
    }
}
