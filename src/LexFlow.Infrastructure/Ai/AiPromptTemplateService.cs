using System.Reflection;
using LexFlow.Application.Common.Interfaces;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LexFlow.Infrastructure.Ai;

/// <summary>
/// §35 Model governance: "prompt templates versioned in repo (/ai/prompts/*.yaml)". Loads every
/// embedded ai/prompts/*.yaml file once at construction and caches it — see
/// LexFlow.Infrastructure.csproj's EmbeddedResource include for how the repo-root files end up
/// in the assembly manifest.
/// </summary>
public sealed class AiPromptTemplateService : IAiPromptTemplateService
{
    private const string ResourcePrefix = "LexFlow.Infrastructure.Ai.Prompts.";

    private readonly IReadOnlyDictionary<string, AiPromptTemplate> _templates;

    public AiPromptTemplateService()
    {
        _templates = LoadAll();
    }

    public AiPromptTemplate Get(string key) =>
        _templates.TryGetValue(key, out var template)
            ? template
            : throw new InvalidOperationException($"No AI prompt template registered for key '{key}' — expected an embedded ai/prompts/{key}.yaml.");

    private static IReadOnlyDictionary<string, AiPromptTemplate> LoadAll()
    {
        var deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
        var assembly = Assembly.GetExecutingAssembly();
        var result = new Dictionary<string, AiPromptTemplate>();

        foreach (var resourceName in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && n.EndsWith(".yaml", StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
            using var reader = new StreamReader(stream);
            var raw = deserializer.Deserialize<RawTemplate>(reader);
            var template = new AiPromptTemplate(raw.Key, raw.Version, raw.Model, raw.MaxTokens, raw.Temperature, raw.SystemPrompt.Trim(), raw.UserTemplate.Trim());
            result[template.Key] = template;
        }

        return result;
    }

    private sealed class RawTemplate
    {
        public string Key { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int MaxTokens { get; set; }
        public double Temperature { get; set; }
        public string SystemPrompt { get; set; } = string.Empty;
        public string UserTemplate { get; set; } = string.Empty;
    }
}
