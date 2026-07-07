namespace LexFlow.Infrastructure.Caching;

/// <summary>Bound from configuration section "Redis". Backs permission-set/settings/dashboard caches per PRD §31.</summary>
public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "localhost:6379";
}
