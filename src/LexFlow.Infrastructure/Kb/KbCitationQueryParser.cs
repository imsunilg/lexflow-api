using System.Text.RegularExpressions;

namespace LexFlow.Infrastructure.Kb;

/// <summary>
/// Module 12 User Flow #4: "section-number direct jump ('IPC 420' -&gt; section); citation-format
/// search ('(2023) 5 SCC 1')." Validation Rules: "Citation format validated against known patterns
/// (SCC/AIR/SCR/neutral) with free-form fallback flag." Pure/static so it is directly unit-testable
/// without any DB or ES dependency — this is the piece AC-KB1's &lt; 500 ms budget depends on being
/// fast, since a recognized section lookup bypasses Elasticsearch entirely (see KbSearchService).
/// </summary>
public static class KbCitationQueryParser
{
    // "(2023) 5 SCC 1"
    private static readonly Regex SccPattern = new(@"^\((?<year>\d{4})\)\s*(?<vol>\d+)\s*SCC\s*(?<page>\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "AIR 2020 SC 123"
    private static readonly Regex AirPattern = new(@"^AIR\s+(?<year>\d{4})\s+(?<court>[A-Za-z]+)\s+(?<page>\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "[2019] 3 SCR 45"
    private static readonly Regex ScrPattern = new(@"^\[(?<year>\d{4})\]\s*(?<vol>\d+)\s*SCR\s*(?<page>\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Neutral citation, e.g. "2023 INSC 123" or "2023 SCC OnLine SC 123".
    private static readonly Regex NeutralPattern = new(@"^(?<year>\d{4})\s+(?<code>[A-Za-z]+(?:\s+OnLine\s+[A-Za-z]+)?)\s+(?<num>\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "IPC 420", "Companies Act 197" — a letters-only act reference followed by a section number
    // (digits with an optional single/double-letter suffix, e.g. "420A").
    private static readonly Regex SectionLookupPattern = new(@"^(?<act>[A-Za-z][A-Za-z.]*(?:\s[A-Za-z.]+){0,4})\s+(?<number>\d+[A-Za-z]{0,2})$", RegexOptions.Compiled);

    public static KbParsedQuery Parse(string rawQuery)
    {
        var trimmed = rawQuery.Trim();

        foreach (var (regex, format) in CitationPatterns)
        {
            if (regex.IsMatch(trimmed))
            {
                return new KbParsedQuery(KbQueryKind.Citation, null, null, trimmed, format);
            }
        }

        var sectionMatch = SectionLookupPattern.Match(trimmed);
        if (sectionMatch.Success)
        {
            return new KbParsedQuery(KbQueryKind.SectionLookup, sectionMatch.Groups["act"].Value.Trim(), sectionMatch.Groups["number"].Value.Trim(), trimmed, null);
        }

        return new KbParsedQuery(KbQueryKind.Freeform, null, null, trimmed, null);
    }

    private static readonly (Regex Regex, string Format)[] CitationPatterns =
    [
        (SccPattern, "SCC"),
        (AirPattern, "AIR"),
        (ScrPattern, "SCR"),
        (NeutralPattern, "Neutral"),
    ];
}

public enum KbQueryKind
{
    SectionLookup,
    Citation,
    Freeform,
}

public sealed record KbParsedQuery(KbQueryKind Kind, string? ActRef, string? SectionNumber, string RawQuery, string? CitationFormat);
