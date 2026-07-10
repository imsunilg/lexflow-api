namespace LexFlow.Infrastructure.Dms;

/// <summary>Bound from configuration section "ClamAv". Points at the ClamAV daemon sidecar (clamd), spoken via the INSTREAM protocol.</summary>
public sealed class ClamAvOptions
{
    public const string SectionName = "ClamAv";

    public string? Host { get; set; }
    public int Port { get; set; } = 3310;
}
