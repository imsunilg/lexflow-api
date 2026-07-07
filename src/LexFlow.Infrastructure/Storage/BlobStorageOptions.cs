namespace LexFlow.Infrastructure.Storage;

/// <summary>Bound from configuration section "BlobStorage". Documents, exports, PDFs — per-tenant containers per PRD §20(5).</summary>
public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    public string ConnectionString { get; set; } = "UseDevelopmentStorage=true";
}
