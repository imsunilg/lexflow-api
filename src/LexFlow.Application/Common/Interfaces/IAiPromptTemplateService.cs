namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// §35 Model governance: "prompt templates versioned in repo (/ai/prompts/*.yaml), evaluated by
/// golden-set regression suite ... gating releases; model/provider switch per feature via
/// config." Templates are loaded once at startup and cached in memory; editing a template means
/// editing its YAML file and redeploying, which is the point — it goes through code review like
/// any other change, not a runtime-mutable DB row.
/// </summary>
public interface IAiPromptTemplateService
{
    AiPromptTemplate Get(string key);
}

public sealed record AiPromptTemplate(string Key, string Version, string Model, int MaxTokens, double Temperature, string SystemPrompt, string UserTemplate)
{
    /// <summary>Renders {{placeholder}} tokens in <see cref="UserTemplate"/> against the given values. Unresolved placeholders are left as-is rather than throwing — a template authoring mistake should be visible, not swallowed.</summary>
    public string Render(IReadOnlyDictionary<string, string> values)
    {
        var rendered = UserTemplate;
        foreach (var (key, value) in values)
        {
            rendered = rendered.Replace("{{" + key + "}}", value, StringComparison.Ordinal);
        }

        return rendered;
    }
}
