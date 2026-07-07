namespace LexFlow.Infrastructure.Search;

/// <summary>Bound from configuration section "Elasticsearch". Backs global/module search per PRD §26.</summary>
public sealed class ElasticsearchOptions
{
    public const string SectionName = "Elasticsearch";

    public string Uri { get; set; } = "http://localhost:9200";
    public string? ApiKey { get; set; }
}
